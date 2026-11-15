using System;
using UnityEngine;

namespace ChessGame
{
    /// <summary>
    /// Handles 3D animation, physics-like feel, and visual state of an individual chess piece.
    /// Provides smooth parabolic gliding, capture flight to tray, and drag feedback.
    /// </summary>
    public class ChessPieceVisual : MonoBehaviour
    {
        public PieceType type;
        public bool isWhite;
        public Vector2Int gridPos;

        private Vector3 startPos;
        private Vector3 targetPos;
        private float animTimer;
        private float animDuration = 0.28f;
        private float liftHeight = 0.5f;
        private bool isAnimating = false;
        private Action onAnimComplete;

        private Vector3 originalScale;
        private Vector3 targetScale;
        private bool isScaleAnimating = false;

        private bool isDragging = false;
        private Vector3 dragPosition;

        private void Awake()
        {
            originalScale = transform.localScale;
            targetScale = originalScale;
        }

        private void Update()
        {
            if (isDragging)
            {
                // Smoothly interpolate towards drag position
                transform.position = Vector3.Lerp(transform.position, dragPosition, Time.deltaTime * 24f);
                return;
            }

            if (isAnimating)
            {
                animTimer += Time.deltaTime;
                float progress = Mathf.Clamp01(animTimer / animDuration);

                // Smooth cubic ease-in-out curve for horizontal travel
                float smooth = progress * progress * (3f - 2f * progress);
                Vector3 current = Vector3.Lerp(startPos, targetPos, smooth);

                // Parabolic vertical lift arc
                float verticalArc = Mathf.Sin(progress * Mathf.PI) * liftHeight;
                current.y += verticalArc;

                transform.position = current;

                if (isScaleAnimating)
                {
                    transform.localScale = Vector3.Lerp(originalScale, targetScale, smooth);
                }

                if (progress >= 1.0f)
                {
                    isAnimating = false;
                    transform.position = targetPos;
                    if (isScaleAnimating)
                    {
                        transform.localScale = targetScale;
                        isScaleAnimating = false;
                    }
                    Action cb = onAnimComplete;
                    onAnimComplete = null;
                    cb?.Invoke();
                }
            }
        }

        /// <summary>
        /// Animates moving piece smoothly to target world coordinates in a parabolic arc.
        /// </summary>
        public void AnimateMoveTo(Vector3 destination, float duration = 0.28f, float arcHeight = 0.5f, Action onComplete = null)
        {
            isDragging = false;
            startPos = transform.position;
            targetPos = destination;
            animDuration = Mathf.Max(0.05f, duration);
            liftHeight = arcHeight;
            animTimer = 0f;
            isAnimating = true;
            onAnimComplete = onComplete;
        }

        /// <summary>
        /// Animates captured piece flying over to the captured piece tray on the side of the board.
        /// </summary>
        public void AnimateCaptureToTray(Vector3 trayPosition, float duration = 0.40f, float arcHeight = 0.75f, Action onComplete = null)
        {
            isDragging = false;
            startPos = transform.position;
            targetPos = trayPosition;
            animDuration = duration;
            liftHeight = arcHeight;
            animTimer = 0f;
            isAnimating = true;
            isScaleAnimating = true;
            targetScale = originalScale * 0.82f; // Scaled slightly to fit neatly in tray
            onAnimComplete = onComplete;
        }

        /// <summary>
        /// Sets dragging state and hover position.
        /// </summary>
        public void SetDrag(bool dragging, Vector3 hoverWorldPos = default)
        {
            isDragging = dragging;
            if (dragging)
            {
                dragPosition = hoverWorldPos;
                isAnimating = false;
            }
        }

        public void UpdateDragPosition(Vector3 worldPos)
        {
            dragPosition = worldPos;
        }

        /// <summary>
        /// Snaps instantly to world position.
        /// </summary>
        public void SnapTo(Vector3 worldPos)
        {
            isAnimating = false;
            isDragging = false;
            transform.position = worldPos;
        }
    }
}
