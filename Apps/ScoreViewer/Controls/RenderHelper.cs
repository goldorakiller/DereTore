﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using DereTore.Apps.ScoreViewer.Model;

namespace DereTore.Apps.ScoreViewer.Controls {
    internal static class RenderHelper {

        public static void DrawAvatars(RenderParams renderParams) {
            var clientSize = renderParams.ClientSize;
            var centerY = clientSize.Height * BaseLineYPosition;
            foreach (var position in AvatarCenterXEndPositions) {
                var centerX = clientSize.Width * position;
                renderParams.Graphics.FillEllipse(AvatarBrush, centerX - AvatarCircleRadius, centerY - AvatarCircleRadius, AvatarCircleDiameter, AvatarCircleDiameter);
            }
        }

        public static void DrawCeilingLine(RenderParams renderParams) {
            var clientSize = renderParams.ClientSize;
            float p1 = AvatarCenterXStartPositions[0], p5 = AvatarCenterXStartPositions[AvatarCenterXStartPositions.Length - 1];
            float x1 = clientSize.Width * p1, x2 = clientSize.Width * p5;
            var ceilingY = FutureNoteCeiling * clientSize.Height;
            renderParams.Graphics.DrawLine(CeilingPen, x1, ceilingY, x2, ceilingY);
        }

        public static void GetVisibleNotes(double now, List<Note> notes, out int startIndex, out int endIndex) {
            startIndex = -1;
            endIndex = -1;
            var i = 0;
            // Notes which have connection lines should be drawn, but only their lines. Case for holding time exceeds falling time window.
            foreach (var note in notes) {
                if (startIndex < 0 && note.HitTiming > now - PastTimeWindow) {
                    startIndex = i;
                }
                if (note.HitTiming > now + FutureTimeWindow) {
                    break;
                }
                endIndex = i;
                ++i;
            }
        }

        public static void DrawNotes(RenderParams renderParams, IList<Note> notes, int startIndex, int endIndex) {
            if (startIndex < 0) {
                return;
            }
            var selectedNotes = notes.Skip(startIndex).Take(endIndex - startIndex + 1);
            foreach (var note in selectedNotes) {
                switch (note.Type) {
                    case NoteType.TapOrFlick:
                    case NoteType.Hold:
                    case NoteType.Slide:
                        if (IsNoteOnStage(note, renderParams.Now)) {
                            if (note.EditorSelected) {
                                DrawSelectedRect(renderParams, note, Pens.White);
                            } else if (note.EditorSelected2) {
                                DrawSelectedRect(renderParams, note, Pens.LightGreen);
                            }
                        }
                        if (note.IsSync) {
                            DrawSyncLine(renderParams, note, note.SyncPairNote);
                        }
                        break;
                }
                switch (note.Type) {
                    case NoteType.TapOrFlick:
                        if (note.IsFlick) {
                            if (note.HasNextFlick) {
                                DrawFlickLine(renderParams, note, note.NextFlickNote);
                            }
                        }
                        break;
                    case NoteType.Hold:
                        if (note.HasNextHold) {
                            DrawHoldLine(renderParams, note, note.NextHoldNote);
                        }
                        if (note.HasPrevHold) {
                            if (!IsNoteOnStage(note.PrevHoldNote, renderParams.Now)) {
                                DrawHoldLine(renderParams, note.PrevHoldNote, note);
                            }
                        }
                        break;
                    case NoteType.Slide:
                        if (note.HasNextSlide) {
                            DrawSlideLine(renderParams, note, note.NextSlideNote);
                        }
                        if (note.HasPrevSlide) {
                            if (!IsNoteOnStage(note.PrevSlideNote, renderParams.Now)) {
                                DrawSlideLine(renderParams, note.PrevSlideNote, note);
                            }
                        }
                        break;
                }
                switch (note.Type) {
                    case NoteType.TapOrFlick:
                        if (note.FlickType == NoteStatus.Tap) {
                            if (note.IsHoldRelease) {
                                DrawHoldNote(renderParams, note);
                            } else {
                                DrawTapNote(renderParams, note);
                            }
                        } else {
                            DrawFlickNote(renderParams, note);
                        }
                        break;
                    case NoteType.Hold:
                        DrawHoldNote(renderParams, note);
                        break;
                    case NoteType.Slide:
                        DrawSlideNote(renderParams, note);
                        break;
                }
            }
        }

        public static void DrawSelectedRect(RenderParams renderParams, Note note, Pen pen) {
            float x = GetNoteXPosition(renderParams, note), y = GetNoteYPosition(renderParams, note);
            float r = GetNoteRadius(renderParams, note);
            renderParams.Graphics.DrawRectangle(pen, x - r, y - r, r * 2f, r * 2f);
        }

        public static void DrawSyncLine(RenderParams renderParams, Note note1, Note note2) {
            var now = renderParams.Now;
            if (!IsNoteOnStage(note1, now) || !IsNoteOnStage(note2, now)) {
                return;
            }
            float x1 = GetNoteXPosition(renderParams, note1),
                y = GetNoteYPosition(renderParams, note2),
                x2 = GetNoteXPosition(renderParams, note2);
            float r = GetNoteRadius(renderParams, note2);
            float xLeft = Math.Min(x1, x2), xRight = Math.Max(x1, x2);
            renderParams.Graphics.DrawLine(SyncLinePen, xLeft + r, y, xRight - r, y);
        }

        public static void DrawHoldLine(RenderParams renderParams, Note startNote, Note endNote) {
            DrawHoldLine(renderParams, startNote, endNote, HoldLinePen);
        }

        public static void DrawHoldLine(RenderParams renderParams, Note startNote, Note endNote, Pen pen) {
            var graphics = renderParams.Graphics;
            var now = renderParams.Now;
            OnStageStatus s1 = GetNoteOnStageStatus(startNote, now), s2 = GetNoteOnStageStatus(endNote, now);
            if (s1 == s2 && s1 != OnStageStatus.OnStage) {
                return;
            }
            float t1 = GetNoteTransformedTime(renderParams, startNote, true, true);
            float t2 = GetNoteTransformedTime(renderParams, endNote, true, true);
            float tmid = (t1 + t2) * 0.5f;
            float x1 = GetNoteXPosition(renderParams, startNote.FinishPosition, startNote.StartPosition, t1);
            float x2 = GetNoteXPosition(renderParams, endNote.FinishPosition, endNote.StartPosition, t2);
            float xmid = GetNoteXPosition(renderParams, endNote.FinishPosition, endNote.StartPosition, tmid);
            float y1 = GetNoteYPosition(renderParams, t1);
            float y2 = GetNoteYPosition(renderParams, t2);
            float ymid = GetNoteYPosition(renderParams, tmid);
            float xcontrol1, xcontrol2, ycontrol1, ycontrol2;
            GetBezierFromQuadratic(x1, xmid, x2, out xcontrol1, out xcontrol2);
            GetBezierFromQuadratic(y1, ymid, y2, out ycontrol1, out ycontrol2);
            graphics.DrawBezier(pen, x1, y1, xcontrol1, ycontrol1, xcontrol2, ycontrol2, x2, y2);
        }

        public static void DrawSlideLine(RenderParams renderParams, Note startNote, Note endNote) {
            if (endNote.IsFlick) {
                DrawFlickLine(renderParams, startNote, endNote);
                return;
            }
            var now = renderParams.Now;
            if (startNote.IsSlideEnd || IsNoteOnStage(startNote, now)) {
                DrawHoldLine(renderParams, startNote, endNote, SlideLinePen);
                return;
            }
            if (IsNotePassed(startNote, now)) {
                var nextSlideNote = startNote.NextSlideNote;
                if (nextSlideNote == null) {
                    // Actually, here is an example of invalid format. :)
                    DrawHoldLine(renderParams, startNote, endNote, SlideLinePen);
                    return;
                }
                if (IsNotePassed(nextSlideNote, now)) {
                    return;
                }
                var startX = GetEndXByNotePosition(renderParams.ClientSize, startNote.FinishPosition);
                var endX = GetEndXByNotePosition(renderParams.ClientSize, nextSlideNote.FinishPosition);
                var y1 = GetAvatarYPosition(renderParams.ClientSize);
                var x1 = (float)((now - startNote.HitTiming) / (nextSlideNote.HitTiming - startNote.HitTiming)) * (endX - startX) + startX;
                float t1 = GetNoteTransformedTime(renderParams, startNote, true, true);
                float t2 = GetNoteTransformedTime(renderParams, endNote, true, true);
                float tmid = (t1 + t2) * 0.5f;
                float x2 = GetNoteXPosition(renderParams, endNote.FinishPosition, endNote.StartPosition, t2);
                float xmid = GetNoteXPosition(renderParams, endNote.FinishPosition, endNote.StartPosition, tmid);
                float y2 = GetNoteYPosition(renderParams, t2);
                float ymid = GetNoteYPosition(renderParams, tmid);
                float xcontrol1, xcontrol2, ycontrol1, ycontrol2;
                GetBezierFromQuadratic(x1, xmid, x2, out xcontrol1, out xcontrol2);
                GetBezierFromQuadratic(y1, ymid, y2, out ycontrol1, out ycontrol2);
                renderParams.Graphics.DrawBezier(SlideLinePen, x1, y1, xcontrol1, ycontrol1, xcontrol2, ycontrol2, x2, y2);
            }
        }

        public static void DrawFlickLine(RenderParams renderParams, Note startNote, Note endNote) {
            DrawSimpleLine(renderParams, startNote, endNote, FlickLinePen);
        }

        public static void DrawSimpleLine(RenderParams renderParams, Note startNote, Note endNote, Pen pen) {
            var graphics = renderParams.Graphics;
            var now = renderParams.Now;
            OnStageStatus s1 = GetNoteOnStageStatus(startNote, now), s2 = GetNoteOnStageStatus(endNote, now);
            if (s1 != OnStageStatus.OnStage && s2 != OnStageStatus.OnStage && s1 == s2) {
                return;
            }
            float x1, x2, y1, y2;
            GetNotePairPositions(renderParams, startNote, endNote, out x1, out x2, out y1, out y2);
            graphics.DrawLine(pen, x1, y1, x2, y2);
        }

        public static void DrawCommonNoteOutline(RenderParams renderParams, float x, float y, float r) {
            renderParams.Graphics.FillEllipse(NoteCommonFill, x - r, y - r, r * 2, r * 2);
            renderParams.Graphics.DrawEllipse(NoteCommonStroke, x - r, y - r, r * 2, r * 2);
        }

        public static void DrawTapNote(RenderParams renderParams, Note note) {
            if (!IsNoteOnStage(note, renderParams.Now)) {
                return;
            }
            float x = GetNoteXPosition(renderParams, note),
                y = GetNoteYPosition(renderParams, note),
                r = GetNoteRadius(renderParams, note);
            DrawCommonNoteOutline(renderParams, x, y, r);

            var graphics = renderParams.Graphics;
            var r1 = r * ScaleFactor1;
            using (var fill = GetFillBrush(x, y, r, TapNoteShapeFillColors)) {
                graphics.FillEllipse(fill, x - r1, y - r1, r1 * 2, r1 * 2);
            }
            graphics.DrawEllipse(TapNoteShapeStroke, x - r1, y - r1, r1 * 2, r1 * 2);
        }

        public static void DrawFlickNote(RenderParams renderParams, Note note) {
            if (!IsNoteOnStage(note, renderParams.Now)) {
                return;
            }
            if (note.FlickType == NoteStatus.Tap) {
                Debug.Print("WARNING: Tap/hold/slide note requested in DrawFlickNote.");
                return;
            }
            float x = GetNoteXPosition(renderParams, note),
                y = GetNoteYPosition(renderParams, note),
                r = GetNoteRadius(renderParams, note);
            DrawCommonNoteOutline(renderParams, x, y, r);

            var graphics = renderParams.Graphics;
            var r1 = r * ScaleFactor1;
            // Triangle
            var polygon = new PointF[3];
            if (note.FlickType == NoteStatus.FlickLeft) {
                polygon[0] = new PointF(x - r1, y);
                polygon[1] = new PointF(x + r1 / 2, y + r1 / 2 * Sqrt3);
                polygon[2] = new PointF(x + r1 / 2, y - r1 / 2 * Sqrt3);

            } else if (note.FlickType == NoteStatus.FlickRight) {
                polygon[0] = new PointF(x + r1, y);
                polygon[1] = new PointF(x - r1 / 2, y - r1 / 2 * Sqrt3);
                polygon[2] = new PointF(x - r1 / 2, y + r1 / 2 * Sqrt3);
            }
            using (var fill = GetFillBrush(x, y, r, FlickNoteShapeFillOuterColors)) {
                graphics.FillPolygon(fill, polygon);
            }
            graphics.DrawPolygon(FlickNoteShapeStroke, polygon);
        }

        public static void DrawHoldNote(RenderParams renderParams, Note note) {
            if (!IsNoteOnStage(note, renderParams.Now)) {
                return;
            }
            float x = GetNoteXPosition(renderParams, note),
                y = GetNoteYPosition(renderParams, note),
                r = GetNoteRadius(renderParams, note);
            DrawCommonNoteOutline(renderParams, x, y, r);

            var graphics = renderParams.Graphics;
            var r1 = r * ScaleFactor1;
            using (var fill = GetFillBrush(x, y, r, HoldNoteShapeFillOuterColors)) {
                graphics.FillEllipse(fill, x - r1, y - r1, r1 * 2, r1 * 2);
            }
            graphics.DrawEllipse(HoldNoteShapeStroke, x - r1, y - r1, r1 * 2, r1 * 2);
            var r2 = r * ScaleFactor3;
            graphics.FillEllipse(HoldNoteShapeFillInner, x - r2, y - r2, r2 * 2, r2 * 2);
        }

        public static void DrawSlideNote(RenderParams renderParams, Note note) {
            if (note.FlickType != NoteStatus.Tap) {
                DrawFlickNote(renderParams, note);
                return;
            }

            float x, y, r;
            Color[] fillColors;
            var now = renderParams.Now;
            if (note.IsSlideEnd || IsNoteOnStage(note, now)) {
                x = GetNoteXPosition(renderParams, note);
                y = GetNoteYPosition(renderParams, note);
                r = GetNoteRadius(renderParams, note);
                fillColors = note.IsSlideMiddle ? SlideNoteShapeFillOuterTranslucentColors : SlideNoteShapeFillOuterColors;
            } else if (IsNotePassed(note, now)) {
                if (!note.HasNextSlide || IsNotePassed(note.NextSlideNote, now)) {
                    return;
                }
                var nextSlideNote = note.NextSlideNote;
                if (nextSlideNote == null) {
                    // Actually, here is an example of invalid format. :)
                    DrawTapNote(renderParams, note);
                    return;
                } else {
                    var startX = GetEndXByNotePosition(renderParams.ClientSize, note.FinishPosition);
                    var endX = GetEndXByNotePosition(renderParams.ClientSize, nextSlideNote.FinishPosition);
                    y = GetAvatarYPosition(renderParams.ClientSize);
                    x = (float)((now - note.HitTiming) / (nextSlideNote.HitTiming - note.HitTiming)) * (endX - startX) + startX;
                    r = AvatarCircleRadius;
                    fillColors = SlideNoteShapeFillOuterColors;
                }
            } else {
                return;
            }

            DrawCommonNoteOutline(renderParams, x, y, r);
            var graphics = renderParams.Graphics;
            var r1 = r * ScaleFactor1;
            using (var fill = GetFillBrush(x, y, r, fillColors)) {
                graphics.FillEllipse(fill, x - r1, y - r1, r1 * 2, r1 * 2);
            }
            var r2 = r * ScaleFactor3;
            graphics.FillEllipse(SlideNoteShapeFillInner, x - r2, y - r2, r2 * 2, r2 * 2);
            var l = r * SlideNoteStrikeHeightFactor;
            graphics.FillRectangle(SlideNoteShapeFillInner, x - r1 - 1, y - l, r1 * 2 + 2, l * 2);
        }

        public static void GetBezierFromQuadratic(float x1, float xmid, float x4, out float x2, out float x3) {
            float xcontrol = xmid * 2f - (x1 + x4) * 0.5f;
            x2 = (x1 + xcontrol * 2f) / 3f;
            x3 = (x4 + xcontrol * 2f) / 3f;
        }

        public static void GetNotePairPositions(RenderParams renderParams, Note note1, Note note2, out float x1, out float x2, out float y1, out float y2) {
            var now = renderParams.Now;
            var clientSize = renderParams.ClientSize;
            if (IsNotePassed(note1, now)) {
                x1 = GetEndXByNotePosition(clientSize, note1.FinishPosition);
                y1 = GetAvatarYPosition(clientSize);
            } else if (IsNoteComing(note1, now)) {
                x1 = GetStartXByNotePosition(clientSize, renderParams.IsPreview ? note1.StartPosition : note1.FinishPosition);
                y1 = GetBirthYPosition(clientSize);
            } else {
                x1 = GetNoteXPosition(renderParams, note1);
                y1 = GetNoteYPosition(renderParams, note1);
            }
            if (IsNotePassed(note2, now)) {
                x2 = GetEndXByNotePosition(clientSize, note2.FinishPosition);
                y2 = GetAvatarYPosition(clientSize);
            } else if (IsNoteComing(note2, now)) {
                x2 = GetStartXByNotePosition(clientSize, renderParams.IsPreview ? note2.StartPosition : note2.FinishPosition);
                y2 = GetBirthYPosition(clientSize);
            } else {
                x2 = GetNoteXPosition(renderParams, note2);
                y2 = GetNoteYPosition(renderParams, note2);
            }
        }

        public static float NoteTimeTransform(float timeRemainingInWindow) {
            return timeRemainingInWindow / (2f - timeRemainingInWindow);
        }

        public static float NoteXTransform(float timeTransformed) {
            return timeTransformed;
        }

        public static float NoteYTransform(float timeTransformed) {
            return timeTransformed + 2.05128205f * timeTransformed * (1f - timeTransformed);
        }

        public static float GetNoteTransformedTime(RenderParams renderParams, Note note, bool clampComing = false, bool clampPassed = false) {
            var timeRemaining = note.HitTiming - renderParams.Now;
            var timeRemainingInWindow = (float)timeRemaining / FutureTimeWindow;
            if (clampComing && timeRemaining > FutureTimeWindow) {
                timeRemainingInWindow = 1f;
            }
            if (clampPassed && timeRemaining < 0f) {
                timeRemainingInWindow = 0f;
            }
            return NoteTimeTransform(timeRemainingInWindow);
        }

        public static float GetNoteXPosition(RenderParams renderParams, Note note, bool clampComing = false, bool clampPassed = false) {
            var timeTransformed = GetNoteTransformedTime(renderParams, note, clampComing, clampPassed);
            return GetNoteXPosition(renderParams, note.FinishPosition, note.StartPosition, timeTransformed);
        }

        public static float GetNoteXPosition(RenderParams renderParams, NotePosition finishPosition, NotePosition startPosition, float timeTransformed) {
            var clientSize = renderParams.ClientSize;
            var endPos = AvatarCenterXEndPositions[(int)finishPosition - 1] * clientSize.Width;
            var displayStartPosition = renderParams.IsPreview ? startPosition : finishPosition;
            var startPos = AvatarCenterXStartPositions[(int)displayStartPosition - 1] * clientSize.Width;
            return endPos - (endPos - startPos) * NoteXTransform(timeTransformed);
        }

        public static float GetNoteYPosition(RenderParams renderParams, Note note, bool clampComing = false, bool clampPassed = false) {
            var timeTransformed = GetNoteTransformedTime(renderParams, note, clampComing, clampPassed);
            return GetNoteYPosition(renderParams, timeTransformed);
        }

        public static float GetNoteYPosition(RenderParams renderParams, float timeTransformed) {
            var clientSize = renderParams.ClientSize;
            float ceiling = FutureNoteCeiling * clientSize.Height,
                baseLine = BaseLineYPosition * clientSize.Height;
            return baseLine - (baseLine - ceiling) * NoteYTransform(timeTransformed);
        }

        public static float GetNoteRadius(RenderParams renderParams, Note note) {
            var timeRemaining = note.HitTiming - renderParams.Now;
            var timeTransformed = NoteTimeTransform((float)timeRemaining / FutureTimeWindow);
            if (timeTransformed < 0.75f) {
                if (timeTransformed < 0f) {
                    return AvatarCircleRadius;
                } else {
                    return AvatarCircleRadius * (1f - timeTransformed * 0.933333333f);
                }
            } else {
                if (timeTransformed < 1f) {
                    return AvatarCircleRadius * ((1f - timeTransformed) * 1.2f);
                } else {
                    return 0f;
                }
            }
        }

        public static float GetAvatarXPosition(Size clientSize, NotePosition position) {
            return clientSize.Width * AvatarCenterXEndPositions[(int)position - 1];
        }

        public static float GetAvatarYPosition(Size clientSize) {
            return clientSize.Height * BaseLineYPosition;
        }

        public static float GetStartXByNotePosition(Size clientSize, NotePosition position) {
            return clientSize.Width * AvatarCenterXStartPositions[(int)position - 1];
        }

        public static float GetEndXByNotePosition(Size clientSize, NotePosition position) {
            return clientSize.Width * AvatarCenterXEndPositions[(int)position - 1];
        }

        public static float GetBirthYPosition(Size clientSize) {
            return clientSize.Height * FutureNoteCeiling;
        }

        public static OnStageStatus GetNoteOnStageStatus(Note note, double now) {
            if (note.HitTiming < now) {
                return OnStageStatus.Passed;
            }
            if (note.HitTiming > now + FutureTimeWindow) {
                return OnStageStatus.Upcoming;
            }
            return OnStageStatus.OnStage;
        }

        public static bool IsNoteOnStage(Note note, double now) {
            return now <= note.HitTiming && note.HitTiming <= now + FutureTimeWindow;
        }

        public static bool IsNotePassed(Note note, double now) {
            return note.HitTiming < now;
        }

        public static bool IsNoteComing(Note note, double now) {
            return note.HitTiming > now + FutureTimeWindow;
        }

        public enum OnStageStatus {
            Upcoming,
            OnStage,
            Passed
        }

        public static readonly Brush AvatarBrush = Brushes.Firebrick;
        public static readonly Pen CeilingPen = Pens.Red;
        public static readonly Pen HoldLinePen = Pens.Yellow;
        public static readonly Pen SyncLinePen = Pens.DodgerBlue;
        public static readonly Pen FlickLinePen = Pens.OliveDrab;
        public static readonly Pen SlideLinePen = Pens.LightPink;

        public static float FutureTimeWindow = 1f;
        public static readonly float PastTimeWindow = 0.2f;
        public static readonly float AvatarCircleDiameter = 50;
        public static readonly float AvatarCircleRadius = AvatarCircleDiameter / 2;
        public static readonly float[] AvatarCenterXStartPositions = { 0.272363281f, 0.381347656f, 0.5f, 0.618652344f, 0.727636719f };
        public static readonly float[] AvatarCenterXEndPositions = { 0.192382812f, 0.346191406f, 0.5f, 0.653808594f, 0.807617188f };
        public static readonly float BaseLineYPosition = 0.828125f;
        // Then we know the bottom is <BaseLineYPosition + (PastWindow / FutureWindow) * (BaseLineYPosition - Ceiling))>.
        public static readonly float FutureNoteCeiling = 0.21875f;

        private static readonly float NoteShapeStrokeWidth = 1;

        private static readonly float ScaleFactor1 = 0.8f;
        private static readonly float ScaleFactor2 = 0.5f;
        private static readonly float ScaleFactor3 = (float)1 / 3f;
        private static readonly float SlideNoteStrikeHeightFactor = (float)4 / 30;
        private static readonly PointF FillGradientTop = new PointF(0, 0);
        private static readonly PointF FillGradientBottom = new PointF(0, AvatarCircleDiameter * ScaleFactor1);

        public static readonly Pen NoteCommonStroke = new Pen(Color.FromArgb(0x22, 0x22, 0x22), NoteShapeStrokeWidth);
        public static readonly Brush NoteCommonFill = Brushes.White;
        public static readonly Pen TapNoteShapeStroke = new Pen(Color.FromArgb(0xFF, 0x33, 0x66), NoteShapeStrokeWidth);
        public static readonly Color[] TapNoteShapeFillColors = { Color.FromArgb(0xFF, 0x99, 0xBB), Color.FromArgb(0xFF, 0x33, 0x66) };
        public static readonly Pen HoldNoteShapeStroke = new Pen(Color.FromArgb(0xFF, 0xBB, 0x22), NoteShapeStrokeWidth);
        public static readonly Color[] HoldNoteShapeFillOuterColors = { Color.FromArgb(0xFF, 0xDD, 0x66), Color.FromArgb(0xFF, 0xBB, 0x22) };
        public static readonly Brush HoldNoteShapeFillInner = Brushes.White;
        public static readonly Pen FlickNoteShapeStroke = new Pen(Color.FromArgb(0x22, 0x55, 0xBB), NoteShapeStrokeWidth);
        public static readonly Color[] FlickNoteShapeFillOuterColors = { Color.FromArgb(0x88, 0xBB, 0xFF), Color.FromArgb(0x22, 0x55, 0xBB) };
        public static readonly Brush FlickNoteShapeFillInner = Brushes.White;
        public static readonly Color[] SlideNoteShapeFillOuterColors = { Color.FromArgb(0xA5, 0x46, 0xDA), Color.FromArgb(0xE1, 0xA8, 0xFB) };
        public static readonly Color[] SlideNoteShapeFillOuterTranslucentColors = { Color.FromArgb(0x80, 0xA5, 0x46, 0xDA), Color.FromArgb(0x80, 0xE1, 0xA8, 0xFB) };
        public static readonly Brush SlideNoteShapeFillInner = Brushes.White;

        private static LinearGradientBrush GetFillBrush(float x, float y, float r, Color[] colors) {
            var r1 = r * ScaleFactor1;
            var top = new PointF(0, y - r1);
            var bottom = new PointF(0, y + r1);
            return new LinearGradientBrush(top, bottom, colors[0], colors[1]);
        }

        private static readonly float Sqrt3 = (float)Math.Sqrt(3);

    }
}
