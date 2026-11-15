create table public.profiles (
    id uuid primary key references auth.users (id) on delete cascade,
    display_name text not null default 'Player' check (char_length(display_name) between 1 and 40),
    rating integer not null default 1200 check (rating >= 400),
    games_played integer not null default 0 check (games_played >= 0),
    wins integer not null default 0 check (wins >= 0),
    draws integer not null default 0 check (draws >= 0),
    losses integer not null default 0 check (losses >= 0),
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create table public.matches (
    id uuid primary key default gen_random_uuid(),
    unity_session_id text not null unique check (char_length(unity_session_id) between 1 and 128),
    status text not null default 'pending' check (status in ('pending', 'completed', 'disputed')),
    result text check (result in ('white_win', 'black_win', 'draw')),
    result_reason text check (result_reason in (
        'checkmate', 'resignation', 'timeout', 'stalemate', 'fifty_move',
        'threefold', 'insufficient_material'
    )),
    move_count integer check (move_count between 0 and 2000),
    created_at timestamptz not null default now(),
    finished_at timestamptz,
    check (
        (status = 'completed' and result is not null and result_reason is not null and
         move_count is not null and finished_at is not null) or
        (status <> 'completed' and result is null and result_reason is null and
         move_count is null and finished_at is null)
    )
);

create table public.match_players (
    match_id uuid not null,
    player_id uuid not null references auth.users (id) on delete cascade,
    color text not null check (color in ('white', 'black')),
    confirmed_result text not null check (confirmed_result in ('white_win', 'black_win', 'draw')),
    confirmed_reason text not null check (confirmed_reason in (
        'checkmate', 'resignation', 'timeout', 'stalemate', 'fifty_move',
        'threefold', 'insufficient_material'
    )),
    confirmed_moves jsonb not null check (jsonb_typeof(confirmed_moves) = 'array'),
    confirmed_at timestamptz not null default now(),
    primary key (match_id, player_id),
    unique (match_id, color),
    constraint match_players_match_id_fkey foreign key (match_id) references public.matches (id) on delete cascade
);

create table public.match_moves (
    match_id uuid not null,
    move_number integer not null check (move_number between 1 and 2000),
    player_id uuid not null,
    san text not null check (char_length(san) between 1 and 16),
    created_at timestamptz not null default now(),
    primary key (match_id, move_number),
    foreign key (match_id, player_id) references public.match_players (match_id, player_id) on delete cascade
);

create table public.player_ratings (
    player_id uuid primary key references auth.users (id) on delete cascade,
    rating integer not null default 1200 check (rating >= 400),
    games_played integer not null default 0 check (games_played >= 0),
    wins integer not null default 0 check (wins >= 0),
    draws integer not null default 0 check (draws >= 0),
    losses integer not null default 0 check (losses >= 0),
    updated_at timestamptz not null default now()
);

create table public.rating_history (
    id bigint generated always as identity primary key,
    player_id uuid not null references auth.users (id) on delete cascade,
    match_id uuid not null references public.matches (id) on delete cascade,
    previous_rating integer not null check (previous_rating >= 400),
    new_rating integer not null check (new_rating >= 400),
    rating_change integer not null,
    created_at timestamptz not null default now(),
    unique (player_id, match_id)
);

create index matches_status_created_at_idx on public.matches (status, created_at desc);
create index match_players_player_confirmed_idx on public.match_players (player_id, confirmed_at desc);
create index match_moves_player_idx on public.match_moves (player_id, match_id);
create index profiles_leaderboard_idx on public.profiles (rating desc, id);
create index rating_history_player_created_idx on public.rating_history (player_id, created_at desc);

create function public.create_chess_profile_for_user()
returns trigger
language plpgsql
security definer
set search_path = ''
as $$
declare
    requested_name text;
begin
    requested_name := nullif(btrim(new.raw_user_meta_data ->> 'display_name'), '');
    if requested_name is null or char_length(requested_name) > 40 then
        requested_name := 'Player';
    end if;

    insert into public.profiles (id, display_name)
    values (new.id, requested_name)
    on conflict (id) do nothing;

    insert into public.player_ratings (player_id)
    values (new.id)
    on conflict (player_id) do nothing;

    return new;
end;
$$;

create trigger on_auth_user_created_chess_profile
after insert on auth.users
for each row execute function public.create_chess_profile_for_user();

insert into public.profiles (id, display_name)
select id,
       case
           when nullif(btrim(raw_user_meta_data ->> 'display_name'), '') is null or
                char_length(btrim(raw_user_meta_data ->> 'display_name')) > 40 then 'Player'
           else btrim(raw_user_meta_data ->> 'display_name')
       end
from auth.users
on conflict (id) do nothing;

insert into public.player_ratings (player_id)
select id from auth.users
on conflict (player_id) do nothing;

create function public.is_chess_match_participant(target_match_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
    select exists (
        select 1
        from public.match_players
        where match_id = target_match_id
          and player_id = auth.uid()
    );
$$;

create function public.confirm_completed_chess_match(
    p_unity_session_id text,
    p_player_color text,
    p_result text,
    p_reason text,
    p_moves jsonb,
    p_player_id uuid
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
    target_match_id uuid;
    target_status text;
    existing_confirmation public.match_players%rowtype;
    other_confirmation public.match_players%rowtype;
    confirmation_count integer;
    white_player_id uuid;
    black_player_id uuid;
    white_rating integer;
    black_rating integer;
    white_score numeric;
    expected_white_score numeric;
    white_change integer;
    black_change integer;
    new_white_rating integer;
    new_black_rating integer;
    move_item jsonb;
begin
    if p_player_id is null or p_unity_session_id is null or
       char_length(p_unity_session_id) not between 1 and 128 or
       p_player_color is null or p_player_color not in ('white', 'black') or
       p_result is null or p_result not in ('white_win', 'black_win', 'draw') or
       p_reason is null or p_reason not in ('checkmate', 'resignation', 'timeout', 'stalemate',
                        'fifty_move', 'threefold', 'insufficient_material') or
       p_moves is null or jsonb_typeof(p_moves) <> 'array' or
       jsonb_array_length(p_moves) > 2000 then
        raise exception 'Invalid match confirmation';
    end if;

    for move_item in select value from jsonb_array_elements(p_moves) as item(value)
    loop
        if jsonb_typeof(move_item) <> 'string' or
           char_length(move_item #>> '{}') not between 1 and 16 or
           (move_item #>> '{}') !~ '^[a-hKQRBN0-9x+#=O-]+$' then
            raise exception 'Invalid move notation';
        end if;
    end loop;

    insert into public.matches (unity_session_id)
    values (p_unity_session_id)
    on conflict (unity_session_id) do nothing;

    select id, status into target_match_id, target_status
    from public.matches
    where unity_session_id = p_unity_session_id
    for update;

    if target_status <> 'pending' then
        return jsonb_build_object('status', target_status);
    end if;

    select * into existing_confirmation
    from public.match_players
    where match_id = target_match_id and player_id = p_player_id
    for update;

    if found then
        if existing_confirmation.color <> p_player_color or
           existing_confirmation.confirmed_result <> p_result or
           existing_confirmation.confirmed_reason <> p_reason or
           existing_confirmation.confirmed_moves <> p_moves then
            update public.matches set status = 'disputed' where id = target_match_id;
            return jsonb_build_object('status', 'disputed');
        end if;
    else
        if exists (
            select 1 from public.match_players
            where match_id = target_match_id and color = p_player_color
        ) or (select count(*) from public.match_players where match_id = target_match_id) >= 2 then
            update public.matches set status = 'disputed' where id = target_match_id;
            return jsonb_build_object('status', 'disputed');
        end if;

        insert into public.match_players (
            match_id, player_id, color, confirmed_result, confirmed_reason, confirmed_moves
        ) values (
            target_match_id, p_player_id, p_player_color, p_result, p_reason, p_moves
        );
    end if;

    select count(*) into confirmation_count
    from public.match_players where match_id = target_match_id;
    if confirmation_count < 2 then
        return jsonb_build_object('status', 'pending');
    end if;

    select * into existing_confirmation
    from public.match_players where match_id = target_match_id and color = 'white';
    select * into other_confirmation
    from public.match_players where match_id = target_match_id and color = 'black';

    if existing_confirmation.confirmed_result <> other_confirmation.confirmed_result or
       existing_confirmation.confirmed_reason <> other_confirmation.confirmed_reason or
       existing_confirmation.confirmed_moves <> other_confirmation.confirmed_moves then
        update public.matches set status = 'disputed' where id = target_match_id;
        return jsonb_build_object('status', 'disputed');
    end if;

    white_player_id := existing_confirmation.player_id;
    black_player_id := other_confirmation.player_id;

    insert into public.match_moves (match_id, move_number, player_id, san)
    select target_match_id, move_index::integer,
           case when move_index % 2 = 1 then white_player_id else black_player_id end,
           move_value
    from jsonb_array_elements_text(existing_confirmation.confirmed_moves)
         with ordinality as move_rows(move_value, move_index);

    update public.matches
    set status = 'completed',
        result = existing_confirmation.confirmed_result,
        result_reason = existing_confirmation.confirmed_reason,
        move_count = jsonb_array_length(existing_confirmation.confirmed_moves),
        finished_at = now()
    where id = target_match_id;

    perform 1
    from public.player_ratings
    where player_id in (white_player_id, black_player_id)
    order by player_id
    for update;
    select rating into white_rating from public.player_ratings where player_id = white_player_id;
    select rating into black_rating from public.player_ratings where player_id = black_player_id;
    if white_rating is null or black_rating is null then
        raise exception 'A match participant has no rating row';
    end if;

    white_score := case existing_confirmation.confirmed_result
        when 'white_win' then 1
        when 'black_win' then 0
        else 0.5
    end;
    expected_white_score := 1 / (
        1 + pg_catalog.power(10::numeric, (black_rating - white_rating)::numeric / 400)
    );
    white_change := pg_catalog.round(32 * (white_score - expected_white_score))::integer;
    black_change := -white_change;
    new_white_rating := greatest(400, white_rating + white_change);
    new_black_rating := greatest(400, black_rating + black_change);

    update public.player_ratings
    set rating = new_white_rating,
        games_played = games_played + 1,
        wins = wins + case when white_score = 1 then 1 else 0 end,
        draws = draws + case when white_score = 0.5 then 1 else 0 end,
        losses = losses + case when white_score = 0 then 1 else 0 end,
        updated_at = now()
    where player_id = white_player_id;

    update public.player_ratings
    set rating = new_black_rating,
        games_played = games_played + 1,
        wins = wins + case when white_score = 0 then 1 else 0 end,
        draws = draws + case when white_score = 0.5 then 1 else 0 end,
        losses = losses + case when white_score = 1 then 1 else 0 end,
        updated_at = now()
    where player_id = black_player_id;

    update public.profiles
    set rating = new_white_rating,
        games_played = games_played + 1,
        wins = wins + case when white_score = 1 then 1 else 0 end,
        draws = draws + case when white_score = 0.5 then 1 else 0 end,
        losses = losses + case when white_score = 0 then 1 else 0 end,
        updated_at = now()
    where id = white_player_id;

    update public.profiles
    set rating = new_black_rating,
        games_played = games_played + 1,
        wins = wins + case when white_score = 0 then 1 else 0 end,
        draws = draws + case when white_score = 0.5 then 1 else 0 end,
        losses = losses + case when white_score = 1 then 1 else 0 end,
        updated_at = now()
    where id = black_player_id;

    insert into public.rating_history (
        player_id, match_id, previous_rating, new_rating, rating_change
    ) values
        (white_player_id, target_match_id, white_rating, new_white_rating, new_white_rating - white_rating),
        (black_player_id, target_match_id, black_rating, new_black_rating, new_black_rating - black_rating);

    return jsonb_build_object('status', 'completed');
end;
$$;

alter table public.profiles enable row level security;
alter table public.matches enable row level security;
alter table public.match_players enable row level security;
alter table public.match_moves enable row level security;
alter table public.player_ratings enable row level security;
alter table public.rating_history enable row level security;

create policy "Signed-in users can read leaderboard profiles"
on public.profiles for select to authenticated using (true);
create policy "Users can update their own profile name"
on public.profiles for update to authenticated
using (auth.uid() = id) with check (auth.uid() = id);

create policy "Participants can read their matches"
on public.matches for select to authenticated
using (public.is_chess_match_participant(id));
create policy "Participants can read match players"
on public.match_players for select to authenticated
using (public.is_chess_match_participant(match_id));
create policy "Participants can read match moves"
on public.match_moves for select to authenticated
using (public.is_chess_match_participant(match_id));
create policy "Players can read their rating"
on public.player_ratings for select to authenticated
using (auth.uid() = player_id);
create policy "Players can read their rating history"
on public.rating_history for select to authenticated
using (auth.uid() = player_id);

grant usage on schema public to authenticated, service_role;

revoke all on public.profiles, public.matches, public.match_players, public.match_moves,
    public.player_ratings, public.rating_history from public, anon, authenticated;
revoke all on sequence public.rating_history_id_seq from public, anon, authenticated;
grant select on public.profiles, public.matches, public.match_players, public.match_moves,
    public.player_ratings, public.rating_history to authenticated;
grant update (display_name) on public.profiles to authenticated;

revoke all on function public.create_chess_profile_for_user() from public, anon, authenticated;
revoke all on function public.is_chess_match_participant(uuid) from public, anon;
grant execute on function public.is_chess_match_participant(uuid) to authenticated;
revoke all on function public.confirm_completed_chess_match(text, text, text, text, jsonb, uuid)
    from public, anon, authenticated;
grant execute on function public.confirm_completed_chess_match(text, text, text, text, jsonb, uuid)
    to service_role;
