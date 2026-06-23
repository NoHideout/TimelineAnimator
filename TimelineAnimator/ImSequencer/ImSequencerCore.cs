// Originally based on ImSequencer from ImGuizmo
// Copyright (c) 2016-2026 Cedric Guillemet and contributors
// Modifications Copyright (c) 2026 NoHideout
//
// Original code licensed under the MIT License.
// See LICENSES/MIT-ImSequencer.txt

using Dalamud.Bindings.ImGui;
using System.Numerics;
using TimelineAnimator.Data;

namespace TimelineAnimator.ImSequencer
{
    public class ImSequencerCore
    {
        public ImGuiCol Color_Content_Background { get; set; } = ImGuiCol.ChildBg;
        public ImGuiCol Color_Header_Background { get; set; } = ImGuiCol.TableHeaderBg;

        public ImGuiCol Color_Header_Text { get; set; } = ImGuiCol.Text;
        public ImGuiCol Color_Header_Lines { get; set; } = ImGuiCol.Border;
        public ImGuiCol Color_Legend_Text { get; set; } = ImGuiCol.Text;
        public ImGuiCol Color_Stripe_1 { get; set; } = ImGuiCol.FrameBg;
        public ImGuiCol Color_Stripe_2 { get; set; } = ImGuiCol.FrameBgHovered;
        public float Color_Content_Lines_Alpha { get; set; } = 0.188f;
        public float Color_Selection_Alpha { get; set; } = 0.25f;
        public ImGuiCol Color_Keyframe_Hover { get; set; } = ImGuiCol.ResizeGripActive;
        public ImGuiCol Color_Keyframe_Selected { get; set; } = ImGuiCol.PlotLinesHovered;
        public ImGuiCol Color_Keyframe_Default { get; set; } = ImGuiCol.TabHovered;
        public ImGuiCol Color_Playhead { get; set; } = ImGuiCol.ButtonActive;
        public ImGuiCol Color_Playhead_Text { get; set; } = ImGuiCol.Text;

        private struct RenderContext
        {
            public ImDrawListPtr DrawList;
            public ImDrawListPtr ParentDrawList;
            public ImGuiIOPtr IO;
            public Vector2 CanvasPos;
            public Vector2 CanvasSize;
            public Vector2 ContentMin;
            public Vector2 ContentMax;
            public float LegendWidth;
            public float LeftOffset;
            public int ItemHeight;
        }

        public bool Draw(string sequenceName, ImSequencerState state, TimelineSequence sequence, ref int currentFrame,
            ref int selectedEntry, bool modifierHeld)
        {
            var ret = false;
            var io = ImGui.GetIO();
            var ItemHeight = 20;

            float splitterGap = 4f;
            float scrollbarHeight = 14.0f;
            bool requestContextMenu = false;
            bool isSplitterHoveredOrActive = false;

            var visibleTracks = new List<TimelineTrack>();
            var expandedParents = new HashSet<string>();

            var safeTracks = sequence.Tracks;

            foreach (var t in safeTracks)
            {
                bool hasNoLoadedParent = string.IsNullOrEmpty(t.ParentName);
                bool parentIsExpanded = hasNoLoadedParent || expandedParents.Contains(t.ParentName);

                if (parentIsExpanded)
                {
                    visibleTracks.Add(t);
                    if (t.IsExpanded) expandedParents.Add(t.Name);
                }
            }

            ImGui.BeginGroup();
            try
            {
                var parentDrawList = ImGui.GetWindowDrawList();
                var canvasPos = ImGui.GetCursorScreenPos();
                var canvasSize = ImGui.GetContentRegionAvail();

                float maxLegendWidth = Math.Max(50f, canvasSize.X - 50f);
                state.LegendWidth = Math.Clamp(state.LegendWidth, 50f, maxLegendWidth);

                float leftOffset = state.LegendWidth + splitterGap;
                var viewWidthPixels = Math.Max(1f, canvasSize.X - leftOffset - ImGui.GetStyle().ScrollbarSize);

                state.ZoomState.MinViewSpan = Math.Max(state.ZoomState.MinViewSpan, 5);
                CalculateZoomAndSpan(state, sequence, viewWidthPixels);
                int firstFrameUsed = (int)Math.Round(state.ZoomState.ViewMin);

                DrawHeader(state, sequence, canvasPos, canvasSize, leftOffset, ItemHeight);

                ImGui.SetCursorScreenPos(new Vector2(canvasPos.X + state.LegendWidth - 3f, canvasPos.Y));
                ImGui.InvisibleButton("##headerSplitter", new Vector2(8f, ItemHeight));
                if (ImGui.IsItemActive()) state.LegendWidth += io.MouseDelta.X;
                if (ImGui.IsItemHovered() || ImGui.IsItemActive())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
                    isSplitterHoveredOrActive = true;
                }

                float spacingY = ImGui.GetStyle().ItemSpacing.Y;
                var childFrameSize = new Vector2(canvasSize.X,
                    Math.Max(10, canvasSize.Y - ItemHeight - scrollbarHeight - (spacingY * 2)));
                float totalUiHeight = ItemHeight + childFrameSize.Y + scrollbarHeight;

                ImGui.SetCursorScreenPos(new Vector2(canvasPos.X, canvasPos.Y + ItemHeight));
                ImGui.PushStyleColor(ImGuiCol.ChildBg, ImGui.GetColorU32(Color_Content_Background));

                ImGui.BeginChild(sequenceName, childFrameSize, false, ImGuiWindowFlags.AlwaysVerticalScrollbar);
                try
                {
                    var childWidth = ImGui.GetContentRegionAvail().X;
                    var controlHeight = visibleTracks.Count * ItemHeight;

                    ImGui.InvisibleButton("contentBar", new Vector2(childWidth, controlHeight));
                    ImGui.SetItemAllowOverlap();

                    var contentMin = ImGui.GetItemRectMin();
                    var itemMax = ImGui.GetItemRectMax();

                    var contentMax = new Vector2(
                        itemMax.X,
                        Math.Max(itemMax.Y, canvasPos.Y + ItemHeight + childFrameSize.Y)
                    );

                    var ctx = new RenderContext
                    {
                        DrawList = ImGui.GetWindowDrawList(),
                        ParentDrawList = parentDrawList,
                        IO = io,
                        CanvasPos = canvasPos,
                        CanvasSize = canvasSize,
                        ContentMin = contentMin,
                        ContentMax = contentMax,
                        LegendWidth = state.LegendWidth,
                        LeftOffset = leftOffset,
                        ItemHeight = ItemHeight
                    };

                    int totalPossibleStripes = (int)((ctx.ContentMax.Y - ctx.ContentMin.Y) / ctx.ItemHeight);
                    DrawTrackStripes(ctx, Math.Max(visibleTracks.Count, totalPossibleStripes));

                    DrawGridLines(ctx, state, sequence);
                    DrawHeaderTicks(ctx, state, sequence);

                    DrawLegend(ctx, safeTracks, visibleTracks, ref selectedEntry);
                    bool clickedOnKeyframe = DrawKeyframes(ctx, state, safeTracks, visibleTracks, modifierHeld,
                        ref requestContextMenu);

                    if (!state.IsDraggingSplitter)
                    {
                        HandleSelectionAndInput(ctx, state, safeTracks, visibleTracks, ref selectedEntry, modifierHeld,
                            clickedOnKeyframe, ref requestContextMenu);
                        HandlePlayhead(ctx, state, sequence, ref currentFrame, firstFrameUsed);
                    }

                    if (state.IsDragging) ret = ProcessDragging(ctx, state, sequence);

                    float scrollY = ImGui.GetScrollY();
                    ImGui.SetCursorPos(new Vector2(state.LegendWidth - 3f, scrollY));
                    ImGui.InvisibleButton("##timelineSplitter", new Vector2(8f, childFrameSize.Y));
                    if (ImGui.IsItemActive()) state.LegendWidth += io.MouseDelta.X;
                    if (ImGui.IsItemHovered() || ImGui.IsItemActive())
                    {
                        ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
                        isSplitterHoveredOrActive = true;
                    }
                }
                finally
                {
                    ImGui.EndChild();
                    ImGui.PopStyleColor();
                }

                DrawScrollbar(state, viewWidthPixels, leftOffset, scrollbarHeight);

                ImGui.SetCursorScreenPos(new Vector2(canvasPos.X + state.LegendWidth - 3f,
                    canvasPos.Y + ItemHeight + childFrameSize.Y));
                ImGui.InvisibleButton("##scrollSplitter", new Vector2(8f, scrollbarHeight));
                if (ImGui.IsItemActive()) state.LegendWidth += io.MouseDelta.X;
                if (ImGui.IsItemHovered() || ImGui.IsItemActive())
                {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
                    isSplitterHoveredOrActive = true;
                }

                state.LegendWidth = Math.Clamp(state.LegendWidth, 50f, maxLegendWidth);

                uint splitLineColor = isSplitterHoveredOrActive
                    ? ImGui.GetColorU32(ImGuiCol.SeparatorHovered)
                    : ImGui.GetColorU32(Color_Header_Lines);

                parentDrawList.AddLine(
                    new Vector2(canvasPos.X + state.LegendWidth + 1f, canvasPos.Y),
                    new Vector2(canvasPos.X + state.LegendWidth + 1f, canvasPos.Y + totalUiHeight),
                    splitLineColor, 2.0f);
            }
            finally
            {
                ImGui.EndGroup();
            }

            if (requestContextMenu) ImGui.OpenPopup("SequencerContextMenu");

            return ret;
        }

        private void CalculateZoomAndSpan(ImSequencerState state, TimelineSequence sequence, float viewWidthPixels)
        {
            state.ZoomState.ContentMin = sequence.FrameMin;
            state.ZoomState.ContentMax = sequence.FrameMax;
            if (state.ZoomState.ContentMax <= state.ZoomState.ContentMin)
                state.ZoomState.ContentMax = state.ZoomState.ContentMin + 1;

            double contentSpan = state.ZoomState.ContentMax - state.ZoomState.ContentMin;

            if (state.ZoomState.MinViewSpan <= 0) state.ZoomState.MinViewSpan = 1;
            if (state.ZoomState.MinViewSpan > contentSpan) state.ZoomState.MinViewSpan = contentSpan;

            if (state.ZoomState.ViewMax <= state.ZoomState.ViewMin || double.IsNaN(state.ZoomState.ViewMax))
            {
                state.ZoomState.ViewMin = sequence.FrameMin;
                double initialSpan = viewWidthPixels / state.framePixelWidth;
                state.ZoomState.ViewMax = state.ZoomState.ViewMin +
                                          (initialSpan <= 0 || double.IsNaN(initialSpan) ? 100 : initialSpan);
            }

            double viewMinLimit = Math.Max(state.ZoomState.ContentMin,
                state.ZoomState.ContentMax - state.ZoomState.MinViewSpan);
            state.ZoomState.ViewMin = Math.Clamp(state.ZoomState.ViewMin, state.ZoomState.ContentMin, viewMinLimit);

            double viewMaxLimit = Math.Min(state.ZoomState.ContentMax,
                state.ZoomState.ViewMin + state.ZoomState.MinViewSpan);
            state.ZoomState.ViewMax = Math.Clamp(state.ZoomState.ViewMax, viewMaxLimit, state.ZoomState.ContentMax);

            double viewSpan = Math.Max(state.ZoomState.ViewMax - state.ZoomState.ViewMin, 1);
            state.framePixelWidth = (float)(viewWidthPixels / viewSpan);
        }

        private void DrawHeader(ImSequencerState state, TimelineSequence sequence, Vector2 canvasPos,
            Vector2 canvasSize, float leftOffset, int itemHeight)
        {
            float headerWidth = canvasSize.X - ImGui.GetStyle().ScrollbarSize;
            var headerSize = new Vector2(headerWidth, itemHeight);

            ImGui.InvisibleButton("topBar", headerSize);
            ImGui.GetWindowDrawList().AddRectFilled(canvasPos, canvasPos + headerSize,
                ImGui.GetColorU32(Color_Header_Background));

            ImGui.GetWindowDrawList().AddLine(
                new Vector2(canvasPos.X, canvasPos.Y + itemHeight),
                new Vector2(canvasPos.X + headerWidth, canvasPos.Y + itemHeight),
                ImGui.GetColorU32(Color_Header_Lines), 1.5f);
        }

        private void DrawTrackStripes(RenderContext ctx, int visibleTrackCount)
        {
            for (int i = 0; i < visibleTrackCount; i++)
            {
                var col = (i & 1) != 0 ? ImGui.GetColorU32(Color_Stripe_1) : ImGui.GetColorU32(Color_Stripe_2);
                var pos = new Vector2(ctx.ContentMin.X + ctx.LeftOffset, ctx.ContentMin.Y + ctx.ItemHeight * i + 1);
                var sz = new Vector2(ctx.ContentMax.X, pos.Y + ctx.ItemHeight - 1);
                ctx.DrawList.AddRectFilled(pos, sz, col);
            }
        }

        private void DrawGridLines(RenderContext ctx, ImSequencerState state, TimelineSequence sequence)
        {
            var modFrameCount = 5;
            var frameStep = 1;
            while (modFrameCount * state.framePixelWidth < 100)
            {
                modFrameCount *= 2;
                frameStep *= 2;
            }

            uint lineColor = ImGui.GetColorU32(new Vector4(1, 1, 1, Color_Content_Lines_Alpha));

            for (var i = sequence.FrameMin; i <= sequence.FrameMax; i += frameStep)
            {
                var px = (float)(ctx.ContentMin.X + (i - state.ZoomState.ViewMin) * state.framePixelWidth +
                                 ctx.LeftOffset);
                if (px <= ctx.ContentMax.X && px >= ctx.ContentMin.X + ctx.LeftOffset)
                {
                    ctx.DrawList.AddLine(new Vector2(px, ctx.ContentMin.Y), new Vector2(px, ctx.ContentMax.Y),
                        lineColor, 1);
                }
            }
        }

        private void DrawHeaderTicks(RenderContext ctx, ImSequencerState state, TimelineSequence sequence)
        {
            var modFrameCount = 5;
            var frameStep = 1;
            while (modFrameCount * state.framePixelWidth < 100)
            {
                modFrameCount *= 2;
                frameStep *= 2;
            }

            var halfModFrameCount = modFrameCount / 2;

            uint textColor = ImGui.GetColorU32(Color_Header_Text);
            uint lineColor = ImGui.GetColorU32(Color_Header_Lines);

            Action<int, int> drawLine = (i, regionHeight) =>
            {
                var baseIndex = i % modFrameCount == 0 || i == sequence.FrameMax || i == sequence.FrameMin;
                var halfIndex = i % halfModFrameCount == 0;
                var px = (float)(ctx.ContentMin.X + (i - state.ZoomState.ViewMin) * state.framePixelWidth +
                                 ctx.LeftOffset);

                var tiretStart = baseIndex ? 4 : halfIndex ? 10 : 14;
                var tiretEnd = baseIndex ? regionHeight : ctx.ItemHeight;

                if (px <= ctx.ContentMax.X && px >= ctx.ContentMin.X + ctx.LeftOffset)
                {
                    ctx.ParentDrawList.AddLine(new Vector2(px, ctx.CanvasPos.Y + tiretStart),
                        new Vector2(px, ctx.CanvasPos.Y + tiretEnd - 1), lineColor, 1);
                }

                if (baseIndex && px >= ctx.ContentMin.X + ctx.LeftOffset && px <= ctx.ContentMax.X)
                {
                    ctx.ParentDrawList.AddText(new Vector2(px + 3f, ctx.CanvasPos.Y), textColor, $"{i}");
                }
            };

            for (var i = sequence.FrameMin; i <= sequence.FrameMax; i += frameStep) drawLine(i, ctx.ItemHeight);
            drawLine(sequence.FrameMin, ctx.ItemHeight);
            drawLine(sequence.FrameMax, ctx.ItemHeight);
        }

        private void DrawLegend(RenderContext ctx, List<TimelineTrack> safeTracks, List<TimelineTrack> visibleTracks,
            ref int selectedEntry)
        {
            ctx.DrawList.PushClipRect(ctx.ContentMin, new Vector2(ctx.ContentMin.X + ctx.LegendWidth, ctx.ContentMax.Y),
                true);
            try
            {
                for (int i = 0; i < visibleTracks.Count; i++)
                {
                    var track = visibleTracks[i];
                    float indent = track.Depth * 14f;
                    var tPos = new Vector2(ctx.ContentMin.X + 3 + indent, ctx.ContentMin.Y + i * ctx.ItemHeight + 2);
                    float textIndent = 16f;

                    var rowRect = new ImRect(
                        new Vector2(ctx.ContentMin.X, ctx.ContentMin.Y + i * ctx.ItemHeight),
                        new Vector2(ctx.ContentMin.X + ctx.LegendWidth, ctx.ContentMin.Y + (i + 1) * ctx.ItemHeight));

                    var bgCol = (i & 1) != 0 ? ImGui.GetColorU32(Color_Stripe_1) : ImGui.GetColorU32(Color_Stripe_2);
                    ctx.DrawList.AddRectFilled(rowRect.Min, rowRect.Max, bgCol);

                    int absoluteIndex = safeTracks.IndexOf(track);

                    if (selectedEntry == absoluteIndex)
                    {
                        uint selectionColor = ImGui.GetColorU32(new Vector4(1, 1, 1, Color_Selection_Alpha));
                        ctx.DrawList.AddRectFilled(rowRect.Min, rowRect.Max, selectionColor);
                    }

                    bool hoveredArrow = false;

                    if (track.HasChildren)
                    {
                        var center = new Vector2(tPos.X + 6f,
                            ctx.ContentMin.Y + i * ctx.ItemHeight + (ctx.ItemHeight / 2f));
                        float s = 4f;

                        var arrowRect = new ImRect(center - new Vector2(8, 8), center + new Vector2(8, 8));
                        hoveredArrow = arrowRect.Contains(ctx.IO.MousePos);
                        uint color = ImGui.GetColorU32(hoveredArrow ? ImGuiCol.Text : ImGuiCol.TextDisabled);

                        if (hoveredArrow && ImGui.IsMouseClicked(0)) track.IsExpanded = !track.IsExpanded;

                        if (track.IsExpanded)
                        {
                            ctx.DrawList.AddTriangleFilled(
                                center + new Vector2(-s, -s + 2), center + new Vector2(s, -s + 2),
                                center + new Vector2(0, s + 2), color);
                        }
                        else
                        {
                            ctx.DrawList.AddTriangleFilled(
                                center + new Vector2(-s + 2, -s), center + new Vector2(-s + 2, s),
                                center + new Vector2(s + 2, 0), color);
                        }
                    }

                    if (!hoveredArrow && rowRect.Contains(ctx.IO.MousePos) && ImGui.IsMouseClicked(0))
                        selectedEntry = absoluteIndex;

                    ctx.DrawList.AddText(new Vector2(tPos.X + textIndent, tPos.Y), ImGui.GetColorU32(Color_Legend_Text),
                        track.DisplayName ?? track.Name ?? $"#{i + 1}");
                }
            }
            finally
            {
                ctx.DrawList.PopClipRect();
            }

            for (int i = 0; i < visibleTracks.Count; i++)
            {
                int absoluteIndex = safeTracks.IndexOf(visibleTracks[i]);
                if (selectedEntry == absoluteIndex)
                {
                    uint selectionColor = ImGui.GetColorU32(new Vector4(1, 1, 1, Color_Selection_Alpha));
                    ctx.DrawList.AddRectFilled(
                        new Vector2(ctx.ContentMin.X + ctx.LeftOffset, ctx.ContentMin.Y + ctx.ItemHeight * i),
                        new Vector2(ctx.ContentMax.X, ctx.ContentMin.Y + ctx.ItemHeight * (i + 1)), selectionColor);
                }
            }
        }

        private bool DrawKeyframes(RenderContext ctx, ImSequencerState state, List<TimelineTrack> safeTracks,
            List<TimelineTrack> visibleTracks, bool modifierHeld, ref bool requestContextMenu)
        {
            bool clickedOnKeyframe = false;
            ctx.DrawList.PushClipRect(new Vector2(ctx.ContentMin.X + ctx.LeftOffset, ctx.ContentMin.Y), ctx.ContentMax,
                true);
            try
            {
                for (int i = 0; i < visibleTracks.Count; i++)
                {
                    var track = visibleTracks[i];
                    int absoluteIndex = safeTracks.IndexOf(track);
                    float y = ctx.ContentMin.Y + ctx.ItemHeight * i + (ctx.ItemHeight / 2f);

                    foreach (var kf in track.Keyframes.ToList())
                    {
                        float x = (float)(ctx.ContentMin.X + ctx.LeftOffset +
                                          (kf.Frame - state.ZoomState.ViewMin) * state.framePixelWidth);
                        float size = 6f;
                        var keyframeRect = new ImRect(new Vector2(x - size, y - size), new Vector2(x + size, y + size));

                        bool isHovered = keyframeRect.Contains(ctx.IO.MousePos);
                        bool isSelected = state.SelectedKeyframes.Contains(new SelectedKeyframe(absoluteIndex, kf.Id));

                        if (isHovered && ImGui.IsMouseClicked((ImGuiMouseButton)1))
                        {
                            state.contextKeyframe = kf;
                            state.contextTrackIndex = absoluteIndex;
                            state.contextMouseFrame =
                                (int)Math.Round(
                                    (ctx.IO.MousePos.X - (ctx.ContentMin.X + ctx.LeftOffset)) / state.framePixelWidth +
                                    state.ZoomState.ViewMin);
                            requestContextMenu = true;
                            clickedOnKeyframe = true;
                        }

                        if (isHovered && ImGui.IsMouseClicked(0) && !state.IsDragging)
                        {
                            if (!isSelected)
                            {
                                if (!modifierHeld) state.SelectedKeyframes.Clear();
                                state.SelectedKeyframes.Add(new SelectedKeyframe(absoluteIndex, kf.Id));
                            }

                            state.IsDragging = true;
                            state.movingPos = (int)ctx.IO.MousePos.X;
                            clickedOnKeyframe = true;
                        }

                        uint drawColor;
                        if (kf.CustomColor.HasValue)
                        {
                            uint baseCol = kf.CustomColor.Value;
                            if (isSelected) drawColor = LerpColorToWhite(baseCol, 0.6f);
                            else if (isHovered) drawColor = LerpColorToWhite(baseCol, 0.3f);
                            else drawColor = baseCol;
                        }
                        else
                        {
                            drawColor = isSelected ? ImGui.GetColorU32(Color_Keyframe_Selected) :
                                isHovered ? ImGui.GetColorU32(Color_Keyframe_Hover) :
                                ImGui.GetColorU32(Color_Keyframe_Default);
                        }

                        if (x >= ctx.ContentMin.X + ctx.LeftOffset && x <= ctx.ContentMax.X)
                        {
                            switch (kf.Shape)
                            {
                                case KeyframeShape.Circle:
                                    ctx.DrawList.AddCircleFilled(new Vector2(x, y), size, drawColor);
                                    break;
                                case KeyframeShape.Square:
                                    ctx.DrawList.AddRectFilled(new Vector2(x - size, y - size),
                                        new Vector2(x + size, y + size), drawColor);
                                    break;
                                case KeyframeShape.Diamond:
                                default:
                                    var p = new Vector2[]
                                        { new(x, y - size), new(x + size, y), new(x, y + size), new(x - size, y) };
                                    ctx.DrawList.AddConvexPolyFilled(ref p[0], p.Length, drawColor);
                                    break;
                            }
                        }
                    }
                }
            }
            finally
            {
                ctx.DrawList.PopClipRect();
            }

            return clickedOnKeyframe;
        }

        private uint LerpColorToWhite(uint color, float amount)
        {
            Vector4 c = ImGui.ColorConvertU32ToFloat4(color);
            c.X += (1.0f - c.X) * amount;
            c.Y += (1.0f - c.Y) * amount;
            c.Z += (1.0f - c.Z) * amount;
            return ImGui.ColorConvertFloat4ToU32(c);
        }

        private void HandleSelectionAndInput(RenderContext ctx, ImSequencerState state, List<TimelineTrack> safeTracks,
            List<TimelineTrack> visibleTracks, ref int selectedEntry, bool modifierHeld, bool clickedOnKeyframe,
            ref bool requestContextMenu)
        {
            var contentRect = new ImRect(ctx.ContentMin, ctx.ContentMax);
            bool clickedOnContent = ImGui.IsMouseClicked(0) && contentRect.Contains(ctx.IO.MousePos) &&
                                    ImGui.IsWindowFocused();
            bool clickedOnTrackArea = clickedOnContent && ctx.IO.MousePos.X > ctx.ContentMin.X + ctx.LeftOffset;

            if (state.IsBoxSelecting)
            {
                state.BoxSelectionEnd = ctx.IO.MousePos;
                ctx.DrawList.AddRectFilled(state.BoxSelectionStart, state.BoxSelectionEnd,
                    ImGui.GetColorU32(new Vector4(1, 1, 1, Color_Selection_Alpha)));

                if (!ctx.IO.MouseDown[0])
                {
                    state.IsBoxSelecting = false;
                    FinalizeBoxSelection(ctx, state, safeTracks, visibleTracks);
                }
            }
            else if (clickedOnTrackArea && !state.MovingCurrentFrame && !clickedOnKeyframe && !state.IsDragging)
            {
                state.BoxSelectionStart = ctx.IO.MousePos;
                state.IsBoxSelecting = true;
                if (!modifierHeld) state.SelectedKeyframes.Clear();

                int clickedRow = (int)((ctx.IO.MousePos.Y - ctx.ContentMin.Y) / ctx.ItemHeight);
                if (clickedRow >= 0 && clickedRow < visibleTracks.Count)
                {
                    selectedEntry = safeTracks.IndexOf(visibleTracks[clickedRow]);
                }
            }

            if (ImGui.IsMouseClicked((ImGuiMouseButton)1) && contentRect.Contains(ctx.IO.MousePos) &&
                !clickedOnKeyframe)
            {
                int hoveredRow = (int)((ctx.IO.MousePos.Y - ctx.ContentMin.Y) / ctx.ItemHeight);
                state.contextTrackIndex = hoveredRow >= 0 && hoveredRow < visibleTracks.Count
                    ? safeTracks.IndexOf(visibleTracks[hoveredRow])
                    : -1;
                state.contextMouseFrame =
                    (int)Math.Round((ctx.IO.MousePos.X - (ctx.ContentMin.X + ctx.LeftOffset)) / state.framePixelWidth +
                                    state.ZoomState.ViewMin);
                state.contextKeyframe = null;
                requestContextMenu = true;
            }
        }

        private void FinalizeBoxSelection(RenderContext ctx, ImSequencerState state, List<TimelineTrack> safeTracks,
            List<TimelineTrack> visibleTracks)
        {
            var minX = Math.Min(state.BoxSelectionStart.X, state.BoxSelectionEnd.X);
            var maxX = Math.Max(state.BoxSelectionStart.X, state.BoxSelectionEnd.X);
            var minY = Math.Min(state.BoxSelectionStart.Y, state.BoxSelectionEnd.Y);
            var maxY = Math.Max(state.BoxSelectionStart.Y, state.BoxSelectionEnd.Y);
            var selectionRect = new ImRect(new Vector2(minX, minY), new Vector2(maxX, maxY));

            for (int i = 0; i < visibleTracks.Count; i++)
            {
                var track = visibleTracks[i];
                int absoluteIndex = safeTracks.IndexOf(track);
                float y = ctx.ContentMin.Y + ctx.ItemHeight * i + (ctx.ItemHeight / 2f);

                foreach (var kf in track.Keyframes.ToList())
                {
                    float x = (float)(ctx.ContentMin.X + ctx.LeftOffset +
                                      (kf.Frame - state.ZoomState.ViewMin) * state.framePixelWidth);
                    var kfRect = new ImRect(new Vector2(x - 6f, y - 6f), new Vector2(x + 6f, y + 6f));

                    if (selectionRect.Overlaps(kfRect))
                        state.SelectedKeyframes.Add(new SelectedKeyframe(absoluteIndex, kf.Id));
                }
            }
        }

        private void HandlePlayhead(RenderContext ctx, ImSequencerState state, TimelineSequence sequence,
            ref int currentFrame, int firstFrameUsed)
        {
            var topRect = new ImRect(new Vector2(ctx.CanvasPos.X + ctx.LeftOffset, ctx.CanvasPos.Y),
                new Vector2(ctx.CanvasPos.X + ctx.CanvasSize.X, ctx.CanvasPos.Y + ctx.ItemHeight));

            if (!state.IsDragging && !state.IsBoxSelecting && topRect.Contains(ctx.IO.MousePos) && ctx.IO.MouseDown[0])
                state.MovingCurrentFrame = true;

            if (state.MovingCurrentFrame)
            {
                state.SelectedKeyframes.Clear();

                int safeMin = Math.Min(sequence.FrameMin, sequence.FrameMax);
                int safeMax = Math.Max(sequence.FrameMin, sequence.FrameMax);

                int calculatedFrame = state.framePixelWidth > 0
                    ? (int)((ctx.IO.MousePos.X - topRect.Min.X) / state.framePixelWidth) + firstFrameUsed
                    : safeMin;

                currentFrame = Math.Clamp(calculatedFrame, safeMin, safeMax);

                if (!ctx.IO.MouseDown[0]) state.MovingCurrentFrame = false;
            }

            var cursorOffset = (float)(ctx.ContentMin.X + ctx.LeftOffset +
                                       (currentFrame - state.ZoomState.ViewMin) * state.framePixelWidth);
            if (cursorOffset >= ctx.ContentMin.X + ctx.LeftOffset && cursorOffset <= ctx.ContentMax.X)
            {
                ctx.DrawList.AddLine(new Vector2(cursorOffset, ctx.ContentMin.Y),
                    new Vector2(cursorOffset, ctx.ContentMax.Y), ImGui.GetColorU32(Color_Playhead), 1f);

                uint playheadColor = ImGui.GetColorU32(Color_Playhead);
                uint textColor = ImGui.GetColorU32(Color_Playhead_Text);
                float headerBottom = ctx.CanvasPos.Y + ctx.ItemHeight;
                float rounding = 3f;
                float padding = 4f;
                string frameText = $"{currentFrame}";
                var textSize = ImGui.CalcTextSize(frameText);
                float triHeight = 5f;
                float boxHeight = textSize.Y + (padding / 2);
                float boxWidthHalf = (textSize.X / 2) + padding;
                float triBaseY = headerBottom - triHeight - 1f;
                float boxBottom = triBaseY;
                float boxTop = boxBottom - boxHeight;

                var boxMin = new Vector2(cursorOffset - boxWidthHalf, boxTop);
                var boxMax = new Vector2(cursorOffset + boxWidthHalf, boxBottom);

                ctx.ParentDrawList.AddRectFilled(boxMin, boxMax, playheadColor, rounding,
                    ImDrawFlags.RoundCornersTopLeft | ImDrawFlags.RoundCornersTopRight);

                var triP1 = new Vector2(cursorOffset - boxWidthHalf, boxBottom);
                var triP2 = new Vector2(cursorOffset + boxWidthHalf, boxBottom);
                var triP3 = new Vector2(cursorOffset, headerBottom - 1f);
                ctx.ParentDrawList.AddTriangleFilled(triP1, triP2, triP3, playheadColor);

                ctx.ParentDrawList.AddText(new Vector2(cursorOffset - (textSize.X / 2), boxTop + (padding / 4)),
                    textColor, frameText);
            }
        }

        private bool ProcessDragging(RenderContext ctx, ImSequencerState state, TimelineSequence sequence)
        {
            ImGui.SetNextFrameWantCaptureMouse(true);
            var diffX = (int)ctx.IO.MousePos.X - state.movingPos;
            var diffFrame = (int)Math.Round(diffX / state.framePixelWidth);
            bool ret = false;

            if (Math.Abs(diffFrame) > 0)
            {
                bool canMove = state.SelectedKeyframes.All(sk =>
                {
                    var kf = sequence.GetTrack(sk.trackIndex)?.Keyframes.FirstOrDefault(k => k.Id == sk.keyframeId);
                    return kf != null && kf.Frame + diffFrame >= sequence.FrameMin &&
                           kf.Frame + diffFrame <= sequence.FrameMax;
                });

                if (canMove)
                {
                    foreach (var sk in state.SelectedKeyframes)
                    {
                        var kf = sequence.GetTrack(sk.trackIndex)?.Keyframes.FirstOrDefault(k => k.Id == sk.keyframeId);
                        if (kf != null) kf.Frame += diffFrame;
                    }

                    state.movingPos += (int)Math.Round(diffFrame * state.framePixelWidth);
                    ret = true;
                }
            }

            if (!ctx.IO.MouseDown[0])
            {
                var selectedIds = state.SelectedKeyframes.Select(sk => sk.keyframeId).ToHashSet();

                foreach (var trackIndex in state.SelectedKeyframes.Select(x => x.trackIndex).Distinct())
                {
                    var track = sequence.GetTrack(trackIndex);
                    if (track != null)
                    {
                        var frameGroups = track.Keyframes.GroupBy(k => k.Frame);
                        var toDelete = new HashSet<Guid>();

                        foreach (var group in frameGroups)
                        {
                            if (group.Count() > 1)
                            {
                                var selectedInGroup = group.Where(k => selectedIds.Contains(k.Id)).ToList();
                                var unselectedInGroup = group.Where(k => !selectedIds.Contains(k.Id)).ToList();

                                if (selectedInGroup.Count == 1 && unselectedInGroup.Any())
                                    foreach (var kf in unselectedInGroup)
                                        toDelete.Add(kf.Id);
                            }
                        }

                        track.Keyframes.RemoveAll(k => toDelete.Contains(k.Id));
                        track.Keyframes = track.Keyframes.OrderBy(k => k.Frame).ToList();
                    }
                }

                state.IsDragging = false;
            }

            return ret;
        }

        private void DrawScrollbar(ImSequencerState state, float viewWidthPixels, float leftOffset, float height)
        {
            var scrollbarCursorPos = ImGui.GetCursorPos();
            ImGui.SetCursorPosX(scrollbarCursorPos.X + leftOffset);
            ImGui.SetItemAllowOverlap();

            if (ZoomScrollbar.Draw("sequencer_zoom", ref state.ZoomState, height))
            {
                double viewSpan = Math.Max(state.ZoomState.ViewMax - state.ZoomState.ViewMin, 1);
                state.framePixelWidth = (float)(viewWidthPixels / viewSpan);
            }

            ImGui.SetCursorPos(new Vector2(scrollbarCursorPos.X, scrollbarCursorPos.Y + height));
        }
    }
}