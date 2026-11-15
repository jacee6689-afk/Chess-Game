using System;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Generates a realistic tabletop tournament and grandmaster study environment around the chess board.
    /// Features:
    /// - Polished walnut hardwood table with chamfered edge and solid turned legs
    /// - Tournament forest-green felt desk mat with brass/gold foil inlay trim
    /// - Retro dual-dial analog chess clock with mechanical plungers, dials, and brass nameplate
    /// - Ceramic coffee mug on cork coaster with dark roast coffee
    /// - FIDE scoresheet notebook with ruled columns, silk ribbon bookmark, and wooden pencil
    /// - Classic tournament chairs on White and Black sides
    /// - Elegant study room backdrop with four wainscoted walls, framed chess art, bookshelves with colored volumes, and parquet floor
    /// - Overhead warm study pendant lamp casting cozy atmospheric spotlighting
    /// </summary>
    public static class ChessEnvironment
    {
        [Serializable]
        public sealed class Settings
        {
            [Header("Palette")]
            [Tooltip("Tint for the polished walnut tabletop.")]
            public Color tableWoodColor = new Color(0.24f, 0.15f, 0.10f);
            [Tooltip("Base color of the tournament felt surrounding the board.")]
            public Color feltColor = new Color(0.08f, 0.20f, 0.14f);
            [Tooltip("Deep slate color for the study room walls.")]
            public Color wallColor = new Color(0.09f, 0.11f, 0.15f);
            [Tooltip("Warm brass used for the mat inlay and decorative metalwork.")]
            public Color brassColor = new Color(0.86f, 0.72f, 0.32f);

            [Header("Surface Finish")]
            [Range(0f, 1f), Tooltip("Reflectivity of the walnut tabletop; lower values keep the finish understated.")]
            public float woodSmoothness = 0.72f;
            [Range(0f, 1f), Tooltip("Keep the felt matte so pieces remain the visual focus.")]
            public float feltSmoothness = 0.22f;
            [Range(0f, 1f), Tooltip("Reflectivity of the brass accents.")]
            public float brassSmoothness = 0.88f;
            [Range(0f, 1f), Tooltip("Metallic response of the brass accents.")]
            public float brassMetallic = 0.75f;

            [Header("Surface Detail")]
            [Range(0.5f, 2f), Tooltip("Scale of the procedural walnut grain.")]
            public float woodGrainScale = 1f;
            [Range(0.5f, 1.5f), Tooltip("Contrast of the walnut grain.")]
            public float woodGrainContrast = 1f;
            [Range(0f, 1.5f), Tooltip("Strength of the subtle woven felt texture.")]
            public float feltWeaveStrength = 0.8f;
            [Range(2f, 32f), Tooltip("Number of felt texture repeats across the mat.")]
            public float feltTextureTiling = 12f;
        }

        public static GameObject Build(Transform parent, Bounds playSurfaceBounds, float squareWidth, Settings settings = null)
        {
            if (settings == null) settings = new Settings();

            GameObject envRoot = new GameObject("Tabletop Environment");
            envRoot.transform.SetParent(parent, false);

            float boardSpan = Mathf.Max(playSurfaceBounds.size.x, playSurfaceBounds.size.z);
            Vector3 center = playSurfaceBounds.center;
            float tableTopY = playSurfaceBounds.min.y - 0.08f;
            float floorY = tableTopY - boardSpan * 0.95f;

            // Generate procedural textures
            Texture2D walnutTex = CreateWalnutTexture(settings.woodGrainScale, settings.woodGrainContrast);
            Texture2D feltTex = CreateFeltTexture(settings.feltWeaveStrength);
            Texture2D clockDialTex = CreateClockFaceTexture();
            Texture2D scoresheetTex = CreateScoresheetTexture();
            Texture2D framedArtTex = CreateFramedChessArtTexture();
            Texture2D bookshelfTex = CreateBookshelfTexture();

            // Materials
            Material tableMat = MakeLitMaterial(settings.tableWoodColor, settings.woodSmoothness, 0.05f, walnutTex);
            Material matMat = MakeLitMaterial(settings.feltColor, settings.feltSmoothness, 0.0f, feltTex);
            matMat.mainTextureScale = Vector2.one * settings.feltTextureTiling;
            Material goldTrimMat = MakeLitMaterial(settings.brassColor, settings.brassSmoothness, settings.brassMetallic);
            Material clockWoodMat = MakeLitMaterial(new Color(0.30f, 0.18f, 0.12f), 0.55f, 0.0f, walnutTex);
            Material clockFaceMat = MakeLitMaterial(Color.white, 0.35f, 0.0f, clockDialTex);
            Material chromeMat = MakeLitMaterial(new Color(0.90f, 0.92f, 0.96f), 0.95f, 0.92f);
            Material ceramicMat = MakeLitMaterial(new Color(0.12f, 0.18f, 0.28f), 0.85f, 0.1f);
            Material coffeeMat = MakeLitMaterial(new Color(0.12f, 0.08f, 0.05f), 0.92f, 0.0f);
            Material coasterMat = MakeLitMaterial(new Color(0.38f, 0.26f, 0.16f), 0.25f, 0.0f);
            Material paperMat = MakeLitMaterial(Color.white, 0.20f, 0.0f, scoresheetTex);
            Material pencilMat = MakeLitMaterial(new Color(0.92f, 0.68f, 0.16f), 0.45f, 0.0f);
            Material chairLeatherMat = MakeLitMaterial(new Color(0.16f, 0.09f, 0.06f), 0.40f, 0.0f);
            Material darkWoodMat = MakeLitMaterial(new Color(0.14f, 0.09f, 0.06f), 0.50f, 0.0f);
            Material wallMat = MakeLitMaterial(settings.wallColor, 0.30f, 0.0f);
            Material wainscotMat = MakeLitMaterial(new Color(0.16f, 0.10f, 0.07f), 0.45f, 0.0f, walnutTex);
            Material floorMat = MakeLitMaterial(new Color(0.18f, 0.12f, 0.08f), 0.40f, 0.0f, walnutTex);

            // =========================================================
            // 1. HARDWOOD WALNUT TABLE
            // =========================================================
            float tableW = boardSpan * 3.4f;
            float tableD = boardSpan * 2.8f;
            float tableThick = boardSpan * 0.18f;

            GameObject table = GameObject.CreatePrimitive(PrimitiveType.Cube);
            table.name = "Hardwood Table Surface";
            table.transform.SetParent(envRoot.transform, false);
            table.transform.position = new Vector3(center.x, tableTopY - tableThick * 0.5f, center.z);
            table.transform.localScale = new Vector3(tableW, tableThick, tableD);
            table.GetComponent<Renderer>().sharedMaterial = tableMat;
            SetIgnoreRaycast(table);

            // Chamfered Bevel Edge Trim
            GameObject tableBevel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tableBevel.name = "Table Bevel Edge";
            tableBevel.transform.SetParent(envRoot.transform, false);
            tableBevel.transform.position = new Vector3(center.x, tableTopY - tableThick * 0.55f, center.z);
            tableBevel.transform.localScale = new Vector3(tableW * 1.025f, tableThick * 0.82f, tableD * 1.025f);
            tableBevel.GetComponent<Renderer>().sharedMaterial = darkWoodMat;
            SetIgnoreRaycast(tableBevel);

            // 4 Turned Table Legs
            float legInsetX = tableW * 0.42f;
            float legInsetZ = tableD * 0.42f;
            float legHeight = tableTopY - tableThick - floorY;
            float legThick = boardSpan * 0.14f;

            for (int lx = -1; lx <= 1; lx += 2)
            {
                for (int lz = -1; lz <= 1; lz += 2)
                {
                    GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    leg.name = $"Table Leg ({lx},{lz})";
                    leg.transform.SetParent(envRoot.transform, false);
                    leg.transform.position = new Vector3(center.x + lx * legInsetX, floorY + legHeight * 0.5f, center.z + lz * legInsetZ);
                    leg.transform.localScale = new Vector3(legThick, legHeight * 0.5f, legThick);
                    leg.GetComponent<Renderer>().sharedMaterial = darkWoodMat;
                    SetIgnoreRaycast(leg);
                }
            }

            // =========================================================
            // 2. TOURNAMENT FELT DESK MAT
            // =========================================================
            float matW = boardSpan * 1.72f;
            float matD = boardSpan * 1.34f;

            GameObject feltMat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            feltMat.name = "Tournament Desk Mat";
            feltMat.transform.SetParent(envRoot.transform, false);
            feltMat.transform.position = new Vector3(center.x, tableTopY + 0.02f, center.z);
            feltMat.transform.localScale = new Vector3(matW, 0.04f, matD);
            feltMat.GetComponent<Renderer>().sharedMaterial = matMat;
            SetIgnoreRaycast(feltMat);

            // Fine brass inlay at the mat perimeter.
            float inlayWidth = squareWidth * 0.018f;
            float inlayInset = squareWidth * 0.08f;
            float inlayHeight = squareWidth * 0.012f;
            float inlayY = tableTopY + 0.04f + inlayHeight * 0.5f - 0.001f;
            CreateMatInlay(envRoot.transform, "Desk Mat Brass Inlay North",
                new Vector3(center.x, inlayY, center.z + matD * 0.5f - inlayInset),
                new Vector3(matW - inlayInset * 2f, inlayHeight, inlayWidth), goldTrimMat);
            CreateMatInlay(envRoot.transform, "Desk Mat Brass Inlay South",
                new Vector3(center.x, inlayY, center.z - matD * 0.5f + inlayInset),
                new Vector3(matW - inlayInset * 2f, inlayHeight, inlayWidth), goldTrimMat);
            CreateMatInlay(envRoot.transform, "Desk Mat Brass Inlay East",
                new Vector3(center.x + matW * 0.5f - inlayInset, inlayY, center.z),
                new Vector3(inlayWidth, inlayHeight, matD - inlayInset * 2f), goldTrimMat);
            CreateMatInlay(envRoot.transform, "Desk Mat Brass Inlay West",
                new Vector3(center.x - matW * 0.5f + inlayInset, inlayY, center.z),
                new Vector3(inlayWidth, inlayHeight, matD - inlayInset * 2f), goldTrimMat);

            // =========================================================
            // 3. RETRO DUAL-DIAL ANALOG CHESS CLOCK
            // Placed on right side, safely outside the capture graveyard tray
            // =========================================================
            BuildChessClockProp(envRoot.transform,
                new Vector3(center.x + boardSpan * 0.98f, tableTopY + 0.035f, center.z - boardSpan * 0.15f),
                squareWidth, clockWoodMat, clockFaceMat, chromeMat, goldTrimMat);

            // =========================================================
            // 4. CERAMIC COFFEE MUG ON CORK COASTER
            // Placed on left side, safely outside White's capture graveyard tray
            // =========================================================
            BuildCoffeeMugProp(envRoot.transform,
                new Vector3(center.x - boardSpan * 0.96f, tableTopY + 0.035f, center.z - boardSpan * 0.28f),
                squareWidth, ceramicMat, coffeeMat, coasterMat);

            // =========================================================
            // 5. SCORESHEET NOTEBOOK & PENCIL
            // Placed on left side, front
            // =========================================================
            BuildScoreNotebookProp(envRoot.transform,
                new Vector3(center.x - boardSpan * 0.96f, tableTopY + 0.035f, center.z + boardSpan * 0.22f),
                squareWidth, paperMat, pencilMat, goldTrimMat, darkWoodMat);

            // =========================================================
            // 6. TOURNAMENT ARMCHAIRS (White & Black sides)
            // =========================================================
            BuildChairProp(envRoot.transform,
                new Vector3(center.x, tableTopY - boardSpan * 0.10f, center.z - boardSpan * 1.15f),
                boardSpan, 0f, chairLeatherMat, darkWoodMat, floorY);

            BuildChairProp(envRoot.transform,
                new Vector3(center.x, tableTopY - boardSpan * 0.10f, center.z + boardSpan * 1.15f),
                boardSpan, 180f, chairLeatherMat, darkWoodMat, floorY);

            // =========================================================
            // 7. STUDY ROOM BACKDROP (Enclosed 4 walls, art & bookshelves)
            // =========================================================
            BuildStudyRoom(envRoot.transform, center, boardSpan, tableTopY, floorY,
                wallMat, wainscotMat, floorMat, goldTrimMat, framedArtTex, bookshelfTex);

            // =========================================================
            // 8. OVERHEAD STUDY PENDANT LAMP (Warm spotlight)
            // =========================================================
            BuildStudyLamp(envRoot.transform, center, boardSpan, tableTopY, goldTrimMat, darkWoodMat);

            return envRoot;
        }

        #region Chess Clock Prop

        private static void BuildChessClockProp(Transform parent, Vector3 pos, float sqW,
            Material woodMat, Material faceMat, Material chromeMat, Material brassMat)
        {
            GameObject clockRoot = new GameObject("Analog Chess Clock Prop");
            clockRoot.transform.SetParent(parent, false);
            clockRoot.transform.position = pos;
            clockRoot.transform.rotation = Quaternion.Euler(0f, -24f, 0f); // Angled comfortably toward player

            float clockW = sqW * 1.55f;
            float clockH = sqW * 0.88f;
            float clockD = sqW * 0.62f;

            // Wooden Casing
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Clock Body";
            body.transform.SetParent(clockRoot.transform, false);
            body.transform.localPosition = new Vector3(0f, clockH * 0.5f, 0f);
            body.transform.localScale = new Vector3(clockW, clockH, clockD);
            body.GetComponent<Renderer>().sharedMaterial = woodMat;
            SetIgnoreRaycast(body);

            // Slanted Top Trim
            GameObject topCap = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topCap.name = "Clock Top Cap";
            topCap.transform.SetParent(clockRoot.transform, false);
            topCap.transform.localPosition = new Vector3(0f, clockH + sqW * 0.02f, 0f);
            topCap.transform.localScale = new Vector3(clockW * 1.05f, sqW * 0.04f, clockD * 1.05f);
            topCap.GetComponent<Renderer>().sharedMaterial = brassMat;
            SetIgnoreRaycast(topCap);

            // Left Dial (White player clock)
            float dialDiameter = clockH * 0.65f;
            GameObject leftFace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftFace.name = "Clock Dial (Left)";
            leftFace.transform.SetParent(clockRoot.transform, false);
            leftFace.transform.localPosition = new Vector3(-clockW * 0.25f, clockH * 0.54f, -clockD * 0.51f);
            leftFace.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            leftFace.transform.localScale = new Vector3(dialDiameter, 0.015f, dialDiameter);
            leftFace.GetComponent<Renderer>().sharedMaterial = faceMat;
            SetIgnoreRaycast(leftFace);

            // Right Dial (Black player clock)
            GameObject rightFace = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightFace.name = "Clock Dial (Right)";
            rightFace.transform.SetParent(clockRoot.transform, false);
            rightFace.transform.localPosition = new Vector3(clockW * 0.25f, clockH * 0.54f, -clockD * 0.51f);
            rightFace.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            rightFace.transform.localScale = new Vector3(dialDiameter, 0.015f, dialDiameter);
            rightFace.GetComponent<Renderer>().sharedMaterial = faceMat;
            SetIgnoreRaycast(rightFace);

            // Brass Bezel Rings around Dials
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject bezel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bezel.name = $"Bezel Ring ({side})";
                bezel.transform.SetParent(clockRoot.transform, false);
                bezel.transform.localPosition = new Vector3(side * clockW * 0.25f, clockH * 0.54f, -clockD * 0.505f);
                bezel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                bezel.transform.localScale = new Vector3(dialDiameter * 1.08f, 0.008f, dialDiameter * 1.08f);
                bezel.GetComponent<Renderer>().sharedMaterial = brassMat;
                SetIgnoreRaycast(bezel);
            }

            // Left Chrome Plunger (Pressed DOWN)
            GameObject leftPlunger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            leftPlunger.name = "Left Plunger (Down)";
            leftPlunger.transform.SetParent(clockRoot.transform, false);
            leftPlunger.transform.localPosition = new Vector3(-clockW * 0.25f, clockH + sqW * 0.04f, 0f);
            leftPlunger.transform.localScale = new Vector3(sqW * 0.16f, sqW * 0.05f, sqW * 0.16f);
            leftPlunger.GetComponent<Renderer>().sharedMaterial = chromeMat;
            SetIgnoreRaycast(leftPlunger);

            // Right Chrome Plunger (Sticking UP)
            GameObject rightPlunger = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rightPlunger.name = "Right Plunger (Up)";
            rightPlunger.transform.SetParent(clockRoot.transform, false);
            rightPlunger.transform.localPosition = new Vector3(clockW * 0.25f, clockH + sqW * 0.12f, 0f);
            rightPlunger.transform.localScale = new Vector3(sqW * 0.16f, sqW * 0.13f, sqW * 0.16f);
            rightPlunger.GetComponent<Renderer>().sharedMaterial = chromeMat;
            SetIgnoreRaycast(rightPlunger);

            // Brass Nameplate on Front
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = "Brass Inlay Plate";
            plate.transform.SetParent(clockRoot.transform, false);
            plate.transform.localPosition = new Vector3(0f, clockH * 0.14f, -clockD * 0.515f);
            plate.transform.localScale = new Vector3(clockW * 0.42f, clockH * 0.12f, 0.015f);
            plate.GetComponent<Renderer>().sharedMaterial = brassMat;
            SetIgnoreRaycast(plate);
        }

        #endregion

        #region Coffee Mug Prop

        private static void BuildCoffeeMugProp(Transform parent, Vector3 pos, float sqW,
            Material mugMat, Material coffeeMat, Material coasterMat)
        {
            GameObject mugRoot = new GameObject("Coffee Mug Prop");
            mugRoot.transform.SetParent(parent, false);
            mugRoot.transform.position = pos;
            mugRoot.transform.rotation = Quaternion.Euler(0f, 38f, 0f);

            float mugR = sqW * 0.28f;
            float mugH = sqW * 0.62f;

            // Cork Coaster
            GameObject coaster = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            coaster.name = "Cork Coaster";
            coaster.transform.SetParent(mugRoot.transform, false);
            coaster.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            coaster.transform.localScale = new Vector3(mugR * 2.6f, 0.018f, mugR * 2.6f);
            coaster.GetComponent<Renderer>().sharedMaterial = coasterMat;
            SetIgnoreRaycast(coaster);

            // Ceramic Mug Body
            GameObject mug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mug.name = "Ceramic Mug Body";
            mug.transform.SetParent(mugRoot.transform, false);
            mug.transform.localPosition = new Vector3(0f, mugH * 0.5f + 0.02f, 0f);
            mug.transform.localScale = new Vector3(mugR * 2f, mugH * 0.5f, mugR * 2f);
            mug.GetComponent<Renderer>().sharedMaterial = mugMat;
            SetIgnoreRaycast(mug);

            // Dark Roast Coffee Surface
            GameObject liquid = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            liquid.name = "Dark Roast Coffee";
            liquid.transform.SetParent(mugRoot.transform, false);
            liquid.transform.localPosition = new Vector3(0f, mugH * 0.88f + 0.02f, 0f);
            liquid.transform.localScale = new Vector3(mugR * 1.78f, 0.008f, mugR * 1.78f);
            liquid.GetComponent<Renderer>().sharedMaterial = coffeeMat;
            SetIgnoreRaycast(liquid);

            // Ergonomic Mug Handle
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "Mug Handle";
            handle.transform.SetParent(mugRoot.transform, false);
            handle.transform.localPosition = new Vector3(mugR + sqW * 0.07f, mugH * 0.52f + 0.02f, 0f);
            handle.transform.localScale = new Vector3(sqW * 0.13f, mugH * 0.52f, sqW * 0.07f);
            handle.GetComponent<Renderer>().sharedMaterial = mugMat;
            SetIgnoreRaycast(handle);
        }

        #endregion

        #region Scoresheet & Pencil Prop

        private static void BuildScoreNotebookProp(Transform parent, Vector3 pos, float sqW,
            Material paperMat, Material pencilMat, Material brassMat, Material coverMat)
        {
            GameObject noteRoot = new GameObject("Scoresheet Notebook Prop");
            noteRoot.transform.SetParent(parent, false);
            noteRoot.transform.position = pos;
            noteRoot.transform.rotation = Quaternion.Euler(0f, -12f, 0f);

            float noteW = sqW * 1.40f;
            float noteD = sqW * 1.85f;
            float noteH = sqW * 0.075f;

            // Bound Leather Cover
            GameObject cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.name = "Leather Cover";
            cover.transform.SetParent(noteRoot.transform, false);
            cover.transform.localPosition = new Vector3(0f, noteH * 0.45f, 0f);
            cover.transform.localScale = new Vector3(noteW * 1.05f, noteH * 0.9f, noteD * 1.05f);
            cover.GetComponent<Renderer>().sharedMaterial = coverMat;
            SetIgnoreRaycast(cover);

            // Ruled Score Paper Pages
            GameObject pages = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pages.name = "Scoresheet Pages";
            pages.transform.SetParent(noteRoot.transform, false);
            pages.transform.localPosition = new Vector3(0f, noteH + 0.006f, 0f);
            pages.transform.localScale = new Vector3(noteW, 0.016f, noteD);
            pages.GetComponent<Renderer>().sharedMaterial = paperMat;
            SetIgnoreRaycast(pages);

            // Red Silk Bookmark Ribbon
            GameObject ribbon = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ribbon.name = "Silk Bookmark";
            ribbon.transform.SetParent(noteRoot.transform, false);
            ribbon.transform.localPosition = new Vector3(0f, noteH + 0.015f, noteD * 0.32f);
            ribbon.transform.localScale = new Vector3(noteW * 0.07f, 0.004f, noteD * 0.45f);
            ribbon.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial(new Color(0.80f, 0.16f, 0.16f), 0.5f);
            SetIgnoreRaycast(ribbon);

            // Hexagonal Tournament Pencil
            GameObject pencil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pencil.name = "Wooden Pencil";
            pencil.transform.SetParent(noteRoot.transform, false);
            pencil.transform.localPosition = new Vector3(noteW * 0.38f, noteH + sqW * 0.04f, -noteD * 0.12f);
            pencil.transform.localRotation = Quaternion.Euler(0f, 18f, 90f);
            pencil.transform.localScale = new Vector3(sqW * 0.045f, noteD * 0.42f, sqW * 0.045f);
            pencil.GetComponent<Renderer>().sharedMaterial = pencilMat;
            SetIgnoreRaycast(pencil);

            // Pencil Brass Ferrule
            GameObject ferrule = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ferrule.name = "Pencil Ferrule";
            ferrule.transform.SetParent(pencil.transform, false);
            ferrule.transform.localPosition = new Vector3(0f, 0.92f, 0f);
            ferrule.transform.localScale = new Vector3(1.05f, 0.08f, 1.05f);
            ferrule.GetComponent<Renderer>().sharedMaterial = brassMat;
            SetIgnoreRaycast(ferrule);
        }

        #endregion

        #region Classic Tournament Chairs

        private static void BuildChairProp(Transform parent, Vector3 pos, float boardSpan,
            float yRot, Material leatherMat, Material woodMat, float floorY)
        {
            GameObject chairRoot = new GameObject($"Tournament Chair ({yRot:F0})");
            chairRoot.transform.SetParent(parent, false);
            chairRoot.transform.position = pos;
            chairRoot.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            float chairW = boardSpan * 0.65f;
            float chairD = boardSpan * 0.55f;
            float seatThick = boardSpan * 0.09f;
            float seatY = 0f;

            // Padded Leather Seat
            GameObject seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Chair Seat Cushion";
            seat.transform.SetParent(chairRoot.transform, false);
            seat.transform.localPosition = new Vector3(0f, seatY, 0f);
            seat.transform.localScale = new Vector3(chairW, seatThick, chairD);
            seat.GetComponent<Renderer>().sharedMaterial = leatherMat;
            SetIgnoreRaycast(seat);

            // Wooden Seat Rim
            GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rim.name = "Chair Seat Rim";
            rim.transform.SetParent(chairRoot.transform, false);
            rim.transform.localPosition = new Vector3(0f, seatY - seatThick * 0.45f, 0f);
            rim.transform.localScale = new Vector3(chairW * 1.05f, seatThick * 0.6f, chairD * 1.05f);
            rim.GetComponent<Renderer>().sharedMaterial = woodMat;
            SetIgnoreRaycast(rim);

            // Backrest Cushion
            float backHeight = boardSpan * 0.55f;
            GameObject back = GameObject.CreatePrimitive(PrimitiveType.Cube);
            back.name = "Chair Backrest";
            back.transform.SetParent(chairRoot.transform, false);
            back.transform.localPosition = new Vector3(0f, seatY + backHeight * 0.55f, -chairD * 0.48f);
            back.transform.localScale = new Vector3(chairW * 0.90f, backHeight, chairD * 0.10f);
            back.GetComponent<Renderer>().sharedMaterial = leatherMat;
            SetIgnoreRaycast(back);

            // Backrest Wooden Frame Posts
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = $"Back Post ({side})";
                post.transform.SetParent(chairRoot.transform, false);
                post.transform.localPosition = new Vector3(side * chairW * 0.46f, seatY + backHeight * 0.55f, -chairD * 0.48f);
                post.transform.localScale = new Vector3(boardSpan * 0.05f, backHeight * 0.55f, boardSpan * 0.05f);
                post.GetComponent<Renderer>().sharedMaterial = woodMat;
                SetIgnoreRaycast(post);
            }

            // 4 Turned Wooden Legs down to floor
            float legH = (pos.y - seatThick * 0.5f) - floorY;
            float legW = boardSpan * 0.06f;
            float legOffX = chairW * 0.42f;
            float legOffZ = chairD * 0.42f;

            for (int lx = -1; lx <= 1; lx += 2)
            {
                for (int lz = -1; lz <= 1; lz += 2)
                {
                    GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    leg.name = $"Chair Leg ({lx},{lz})";
                    leg.transform.SetParent(chairRoot.transform, false);
                    leg.transform.localPosition = new Vector3(lx * legOffX, seatY - legH * 0.5f, lz * legOffZ);
                    leg.transform.localScale = new Vector3(legW, legH * 0.5f, legW);
                    leg.GetComponent<Renderer>().sharedMaterial = woodMat;
                    SetIgnoreRaycast(leg);
                }
            }
        }

        #endregion

        #region Study Room Backdrop & Lighting

        private static void CreateMatInlay(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject inlay = GameObject.CreatePrimitive(PrimitiveType.Cube);
            inlay.name = name;
            inlay.transform.SetParent(parent, false);
            inlay.transform.position = position;
            inlay.transform.localScale = scale;
            inlay.GetComponent<Renderer>().sharedMaterial = material;
            SetIgnoreRaycast(inlay);
        }

        private static void BuildStudyRoom(Transform parent, Vector3 center, float boardSpan,
            float tableTopY, float floorY, Material wallMat, Material wainscotMat, Material floorMat,
            Material goldTrimMat, Texture2D framedArtTex, Texture2D bookshelfTex)
        {
            GameObject roomRoot = new GameObject("Study Room Enclosure");
            roomRoot.transform.SetParent(parent, false);

            float wallDistZ = boardSpan * 2.35f;
            float wallDistX = boardSpan * 2.85f;
            float roomWidth = wallDistX * 2.05f;
            float roomDepth = wallDistZ * 2.05f;
            float roomHeight = boardSpan * 2.70f;
            float wallCenterY = floorY + roomHeight * 0.5f;

            // 1. Hardwood Parquet Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Study Parquet Floor";
            floor.transform.SetParent(roomRoot.transform, false);
            floor.transform.position = new Vector3(center.x, floorY - 0.2f, center.z);
            floor.transform.localScale = new Vector3(roomWidth * 1.15f, 0.4f, roomDepth * 1.15f);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            SetIgnoreRaycast(floor);

            // 2. North Wall (Back wall, behind Black)
            GameObject northWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            northWall.name = "North Wall (Back)";
            northWall.transform.SetParent(roomRoot.transform, false);
            northWall.transform.position = new Vector3(center.x, wallCenterY, center.z + wallDistZ);
            northWall.transform.localScale = new Vector3(roomWidth, roomHeight, 1.2f);
            northWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            SetIgnoreRaycast(northWall);

            // 3. South Wall (Front wall, behind White)
            GameObject southWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            southWall.name = "South Wall (Front)";
            southWall.transform.SetParent(roomRoot.transform, false);
            southWall.transform.position = new Vector3(center.x, wallCenterY, center.z - wallDistZ);
            southWall.transform.localScale = new Vector3(roomWidth, roomHeight, 1.2f);
            southWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            SetIgnoreRaycast(southWall);

            // 4. West Wall (Left wall)
            GameObject westWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            westWall.name = "West Wall (Left)";
            westWall.transform.SetParent(roomRoot.transform, false);
            westWall.transform.position = new Vector3(center.x - wallDistX, wallCenterY, center.z);
            westWall.transform.localScale = new Vector3(1.2f, roomHeight, roomDepth);
            westWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            SetIgnoreRaycast(westWall);

            // 5. East Wall (Right wall)
            GameObject eastWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            eastWall.name = "East Wall (Right)";
            eastWall.transform.SetParent(roomRoot.transform, false);
            eastWall.transform.position = new Vector3(center.x + wallDistX, wallCenterY, center.z);
            eastWall.transform.localScale = new Vector3(1.2f, roomHeight, roomDepth);
            eastWall.GetComponent<Renderer>().sharedMaterial = wallMat;
            SetIgnoreRaycast(eastWall);

            // Wainscoting Dado Rails (running along all four walls at chair height)
            float railY = floorY + roomHeight * 0.38f;
            float railThick = boardSpan * 0.06f;

            // North/South Rails
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Wainscot Rail Z ({side})";
                rail.transform.SetParent(roomRoot.transform, false);
                rail.transform.position = new Vector3(center.x, railY, center.z + side * (wallDistZ - 0.5f));
                rail.transform.localScale = new Vector3(roomWidth, railThick, 0.35f);
                rail.GetComponent<Renderer>().sharedMaterial = wainscotMat;
                SetIgnoreRaycast(rail);
            }

            // East/West Rails
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = $"Wainscot Rail X ({side})";
                rail.transform.SetParent(roomRoot.transform, false);
                rail.transform.position = new Vector3(center.x + side * (wallDistX - 0.5f), railY, center.z);
                rail.transform.localScale = new Vector3(0.35f, railThick, roomDepth);
                rail.GetComponent<Renderer>().sharedMaterial = wainscotMat;
                SetIgnoreRaycast(rail);
            }

            // Framed Grandmaster Chess Art Piece on North Wall
            float artW = boardSpan * 1.35f;
            float artH = boardSpan * 0.95f;
            float artY = railY + artH * 0.75f;

            GameObject artFrame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            artFrame.name = "Ornate Gilded Frame";
            artFrame.transform.SetParent(roomRoot.transform, false);
            artFrame.transform.position = new Vector3(center.x, artY, center.z + wallDistZ - 0.52f);
            artFrame.transform.localScale = new Vector3(artW * 1.08f, artH * 1.08f, 0.10f);
            artFrame.GetComponent<Renderer>().sharedMaterial = goldTrimMat;
            SetIgnoreRaycast(artFrame);

            GameObject artCanvas = GameObject.CreatePrimitive(PrimitiveType.Cube);
            artCanvas.name = "Chess Artwork Canvas";
            artCanvas.transform.SetParent(roomRoot.transform, false);
            artCanvas.transform.position = new Vector3(center.x, artY, center.z + wallDistZ - 0.58f);
            artCanvas.transform.localScale = new Vector3(artW, artH, 0.05f);
            artCanvas.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial(Color.white, 0.2f, 0.0f, framedArtTex);
            SetIgnoreRaycast(artCanvas);

            // Wooden Bookshelves with Books on East and West Walls
            Material bookMat = MakeLitMaterial(Color.white, 0.35f, 0.0f, bookshelfTex);
            float shelfW = boardSpan * 1.45f;
            float shelfH = boardSpan * 1.10f;
            float shelfD = boardSpan * 0.32f;

            // West Bookshelf
            BuildBookshelf(roomRoot.transform,
                new Vector3(center.x - wallDistX + shelfD * 0.5f + 0.5f, railY + shelfH * 0.5f, center.z),
                shelfW, shelfH, shelfD, 90f, wainscotMat, bookMat);

            // East Bookshelf
            BuildBookshelf(roomRoot.transform,
                new Vector3(center.x + wallDistX - shelfD * 0.5f - 0.5f, railY + shelfH * 0.5f, center.z),
                shelfW, shelfH, shelfD, -90f, wainscotMat, bookMat);
        }

        private static void BuildBookshelf(Transform parent, Vector3 pos, float w, float h, float d,
            float yRot, Material woodMat, Material bookMat)
        {
            GameObject shelfRoot = new GameObject($"Library Bookshelf ({yRot:F0})");
            shelfRoot.transform.SetParent(parent, false);
            shelfRoot.transform.position = pos;
            shelfRoot.transform.rotation = Quaternion.Euler(0f, yRot, 0f);

            // Outer Frame
            GameObject frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "Shelf Frame";
            frame.transform.SetParent(shelfRoot.transform, false);
            frame.transform.localPosition = Vector3.zero;
            frame.transform.localScale = new Vector3(w, h, d);
            frame.GetComponent<Renderer>().sharedMaterial = woodMat;
            SetIgnoreRaycast(frame);

            // Book Spines Insert
            GameObject books = GameObject.CreatePrimitive(PrimitiveType.Cube);
            books.name = "Books Array";
            books.transform.SetParent(shelfRoot.transform, false);
            books.transform.localPosition = new Vector3(0f, 0f, -d * 0.35f);
            books.transform.localScale = new Vector3(w * 0.92f, h * 0.88f, d * 0.35f);
            books.GetComponent<Renderer>().sharedMaterial = bookMat;
            SetIgnoreRaycast(books);
        }

        private static void BuildStudyLamp(Transform parent, Vector3 center, float boardSpan,
            float tableTopY, Material brassMat, Material darkMat)
        {
            GameObject lampRoot = new GameObject("Overhead Study Pendant Lamp");
            lampRoot.transform.SetParent(parent, false);
            Vector3 lampPos = center + new Vector3(0f, boardSpan * 1.75f, -boardSpan * 0.25f);
            lampRoot.transform.position = lampPos;

            // Brass Suspension Rod
            GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rod.name = "Lamp Brass Rod";
            rod.transform.SetParent(lampRoot.transform, false);
            rod.transform.localPosition = new Vector3(0f, boardSpan * 0.45f, 0f);
            rod.transform.localScale = new Vector3(boardSpan * 0.025f, boardSpan * 0.45f, boardSpan * 0.025f);
            rod.GetComponent<Renderer>().sharedMaterial = brassMat;
            SetIgnoreRaycast(rod);

            // Elegant Green/Brass Shade
            GameObject shade = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shade.name = "Lamp Shade";
            shade.transform.SetParent(lampRoot.transform, false);
            shade.transform.localPosition = Vector3.zero;
            shade.transform.localScale = new Vector3(boardSpan * 0.42f, boardSpan * 0.12f, boardSpan * 0.42f);
            shade.GetComponent<Renderer>().sharedMaterial = MakeLitMaterial(new Color(0.10f, 0.25f, 0.18f), 0.70f, 0.3f);
            SetIgnoreRaycast(shade);

            // Shade Brass Lip Rim
            GameObject lip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            lip.name = "Shade Brass Rim";
            lip.transform.SetParent(lampRoot.transform, false);
            lip.transform.localPosition = new Vector3(0f, -boardSpan * 0.06f, 0f);
            lip.transform.localScale = new Vector3(boardSpan * 0.44f, boardSpan * 0.02f, boardSpan * 0.44f);
            lip.GetComponent<Renderer>().sharedMaterial = brassMat;
            SetIgnoreRaycast(lip);

            // Warm Incandescent Spotlight
            Light spotLight = lampRoot.AddComponent<Light>();
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(1.0f, 0.93f, 0.82f); // Warm incandescent glow (2700K)
            spotLight.intensity = 1.25f;
            spotLight.range = boardSpan * 4.2f;
            spotLight.spotAngle = 68f;
            spotLight.innerSpotAngle = 48f;
            spotLight.shadows = LightShadows.Soft;
        }

        #endregion

        #region Helpers & Procedural Textures

        private static void SetIgnoreRaycast(GameObject obj)
        {
            int ignoreMask = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreMask >= 0) obj.layer = ignoreMask;

            Collider col = obj.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
                UnityEngine.Object.Destroy(col);
            }
        }

        private static Material MakeLitMaterial(Color color, float smoothness = 0.5f, float metallic = 0.0f, Texture2D mainTexture = null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Mobile/Diffuse");

            Material mat = new Material(shader);

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            mat.color = color;

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (mainTexture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", mainTexture);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", mainTexture);
            }

            return mat;
        }

        private static Texture2D CreateWalnutTexture(float grainScale, float grainContrast)
        {
            int w = 256, h = 256;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(x * 0.035f * grainScale, y * 0.22f * grainScale) * 0.65f
                            + Mathf.PerlinNoise(x * 0.08f * grainScale, y * 0.03f * grainScale) * 0.35f;
                    float grainAmount = Mathf.Clamp01(Mathf.PingPong(n * 2.6f, 1f) * grainContrast);
                    float grainTone = n > 0.52f ? 0.96f : 0.66f;
                    float brightness = Mathf.Lerp(0.82f, grainTone, grainAmount);
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness));
                }
            }
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateFeltTexture(float weaveStrength)
        {
            const int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float threadX = Mathf.Sin((x + (y & 1) * 0.5f) * Mathf.PI * 0.5f);
                    float threadY = Mathf.Sin((y + (x & 1) * 0.5f) * Mathf.PI * 0.5f);
                    float fiberNoise = Mathf.PerlinNoise(x * 0.31f, y * 0.31f) - 0.5f;
                    float brightness = Mathf.Clamp01(0.90f + (threadX + threadY) * 0.025f * weaveStrength
                        + fiberNoise * 0.08f * weaveStrength);
                    tex.SetPixel(x, y, new Color(brightness, brightness, brightness));
                }
            }

            tex.Apply(true, false);
            return tex;
        }

        private static Texture2D CreateClockFaceTexture()
        {
            int size = 256;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
            float radius = size * 0.44f;
            Color dialBg = new Color(0.97f, 0.95f, 0.91f, 1f); // Vintage ivory
            Color rimColor = new Color(0.24f, 0.16f, 0.10f, 1f);
            Color tickColor = new Color(0.15f, 0.15f, 0.15f, 1f);
            Color redFlag = new Color(0.85f, 0.18f, 0.14f, 1f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    if (dist > radius)
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                    }
                    else if (dist > radius - 6f)
                    {
                        tex.SetPixel(x, y, rimColor);
                    }
                    else
                    {
                        tex.SetPixel(x, y, dialBg);
                    }
                }
            }

            // Draw 12 Hour ticks
            for (int i = 0; i < 12; i++)
            {
                float angle = i * (Mathf.PI * 2f / 12f);
                float outerR = radius - 8f;
                float innerR = (i % 3 == 0) ? radius - 24f : radius - 16f;
                int stroke = (i % 3 == 0) ? 3 : 2;

                for (float r = innerR; r <= outerR; r += 0.8f)
                {
                    int px = Mathf.RoundToInt(center.x + Mathf.Cos(angle) * r);
                    int py = Mathf.RoundToInt(center.y + Mathf.Sin(angle) * r);
                    for (int dx = -stroke / 2; dx <= stroke / 2; dx++)
                    {
                        for (int dy = -stroke / 2; dy <= stroke / 2; dy++)
                        {
                            tex.SetPixel(px + dx, py + dy, tickColor);
                        }
                    }
                }
            }

            // Red flag at 11 o'clock
            int flagX = Mathf.RoundToInt(center.x - radius * 0.42f);
            int flagY = Mathf.RoundToInt(center.y + radius * 0.52f);
            for (int fx = 0; fx < 14; fx++)
            {
                for (int fy = 0; fy < 10; fy++)
                {
                    tex.SetPixel(flagX + fx, flagY + fy, redFlag);
                }
            }

            // Hands
            DrawThickLine(tex, center, center + new Vector2(-radius * 0.32f, radius * 0.32f), 4, tickColor);
            DrawThickLine(tex, center, center + new Vector2(radius * 0.16f, radius * 0.62f), 3, tickColor);

            // Center Pin
            for (int dx = -5; dx <= 5; dx++)
                for (int dy = -5; dy <= 5; dy++)
                    if (dx * dx + dy * dy <= 25)
                        tex.SetPixel((int)center.x + dx, (int)center.y + dy, rimColor);

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateScoresheetTexture()
        {
            int w = 256, h = 340;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color paperBg = new Color(0.96f, 0.94f, 0.88f);
            Color headerBg = new Color(0.20f, 0.28f, 0.38f);
            Color lineBlue = new Color(0.72f, 0.78f, 0.88f);
            Color spineRed = new Color(0.75f, 0.20f, 0.20f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (x < 14)
                    {
                        tex.SetPixel(x, y, spineRed); // Red margin
                    }
                    else if (y > h - 45)
                    {
                        tex.SetPixel(x, y, headerBg); // Header bar
                    }
                    else if ((y % 16) == 0 || x == 55 || x == 145)
                    {
                        tex.SetPixel(x, y, lineBlue); // Grid / Ruled lines
                    }
                    else
                    {
                        tex.SetPixel(x, y, paperBg);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateFramedChessArtTexture()
        {
            int w = 256, h = 180;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color parchment = new Color(0.93f, 0.89f, 0.80f);
            Color darkEngraving = new Color(0.18f, 0.14f, 0.12f);
            Color goldLeaf = new Color(0.85f, 0.68f, 0.25f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // Border
                    if (x < 8 || x > w - 8 || y < 8 || y > h - 8)
                    {
                        tex.SetPixel(x, y, goldLeaf);
                        continue;
                    }

                    // Chessboard illustration in center
                    int bx = x - 78;
                    int by = y - 40;
                    if (bx >= 0 && bx < 100 && by >= 0 && by < 100)
                    {
                        int sqX = bx / 12;
                        int sqY = by / 12;
                        bool dark = ((sqX + sqY) % 2 == 1);
                        tex.SetPixel(x, y, dark ? darkEngraving : parchment);
                    }
                    else
                    {
                        tex.SetPixel(x, y, parchment);
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static Texture2D CreateBookshelfTexture()
        {
            int w = 256, h = 160;
            Texture2D tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            Color[] bookColors = {
                new Color(0.55f, 0.12f, 0.12f), // Crimson
                new Color(0.14f, 0.22f, 0.38f), // Navy
                new Color(0.12f, 0.34f, 0.18f), // Forest green
                new Color(0.38f, 0.22f, 0.12f), // Leather brown
                new Color(0.24f, 0.14f, 0.28f), // Plum
                new Color(0.70f, 0.55f, 0.20f)  // Ochre gold
            };
            Color woodShelf = new Color(0.20f, 0.13f, 0.08f);
            Color goldFoil = new Color(0.88f, 0.75f, 0.30f);

            int shelfH = 20;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (y < shelfH || (y > h / 2 - 10 && y < h / 2 + 10))
                    {
                        tex.SetPixel(x, y, woodShelf);
                    }
                    else
                    {
                        int bookIdx = (x / 22) % bookColors.Length;
                        Color bc = bookColors[bookIdx];
                        // Spine ribs
                        if ((y % 28) < 3)
                        {
                            tex.SetPixel(x, y, goldFoil);
                        }
                        else
                        {
                            tex.SetPixel(x, y, bc);
                        }
                    }
                }
            }
            tex.Apply();
            return tex;
        }

        private static void DrawThickLine(Texture2D tex, Vector2 p1, Vector2 p2, int thickness, Color col)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(p1, p2) * 1.5f);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector2 p = Vector2.Lerp(p1, p2, t);
                for (int dx = -thickness / 2; dx <= thickness / 2; dx++)
                {
                    for (int dy = -thickness / 2; dy <= thickness / 2; dy++)
                    {
                        tex.SetPixel(Mathf.RoundToInt(p.x) + dx, Mathf.RoundToInt(p.y) + dy, col);
                    }
                }
            }
        }

        #endregion
    }
}
