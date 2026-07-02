using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using TimelineAnimator.Data;

namespace TimelineAnimator.Windows
{
    public class GraphEditorWindow : Window, IDisposable
    {
        private Vector2 pan = new(0, 0);
        private Vector2 zoom = new(10, 50);
        private bool isPanning = false;
        
        private Guid draggingKfId = Guid.Empty;
        private int draggingHandle = -1; // -1 none, 0 Point,1 Out ,2 In
        
        private Guid contextKfId = Guid.Empty;
        private Guid selectedKfId = Guid.Empty;
        private bool alignTangents = true;

        public GraphEditorWindow() : base("Graph Editor###GraphEditorWindow")
        {
            Size = new Vector2(600, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose() { }

        public override void Draw()
        {
            var workspace = Services.WorkspaceService;
            var seq = workspace.GetActiveSequencer() as Sequencers.SequencerBase;
            if (seq == null || workspace.SharedSelectedEntry < 0)
            {
                ImGui.TextDisabled("Select a specific property track (e.g., Position X) to view its graph.");
                return;
            }

            var rows = seq.GetFlattenedRows();
            if (workspace.SharedSelectedEntry >= rows.Count) return;
            
            var row = rows[workspace.SharedSelectedEntry];
            if (row.PropTrack == null)
            {
                ImGui.TextDisabled("Select a specific property track (e.g., Position X) to view its graph.");
                return;
            }

            var track = row.PropTrack;
            var obj = seq.Clip.Objects.FirstOrDefault(o => o.Tracks.Any(t => t.Id == track.Id));
            if (obj == null) return;

            ImGui.Text($"Graph: {obj.Name} - {track.Property}");
            
            ImGui.SameLine(ImGui.GetContentRegionAvail().X - 250);
            ImGui.Checkbox("Align Tangents", ref alignTangents);
            ImGui.SameLine();
            if (ImGui.Button("Reset View"))
            {
                pan = Vector2.Zero;
                zoom = new Vector2(10, 50);
            }

            var canvasPos = ImGui.GetCursorScreenPos();
            var canvasSize = ImGui.GetContentRegionAvail();
            if (canvasSize.X < 50 || canvasSize.Y < 50) return;

            var drawList = ImGui.GetWindowDrawList();
            drawList.AddRectFilled(canvasPos, canvasPos + canvasSize, ImGui.GetColorU32(ImGuiCol.FrameBg));
            drawList.AddRect(canvasPos, canvasPos + canvasSize, ImGui.GetColorU32(ImGuiCol.Border));

            HandleInput(canvasPos, canvasSize);
            DrawGrid(drawList, canvasPos, canvasSize);

            ImGui.PushClipRect(canvasPos, canvasPos + canvasSize, true);
            DrawCurve(drawList, track.Curve, 0xFF00FF00, canvasPos, canvasSize);
            ImGui.PopClipRect();

            ImGui.SetCursorScreenPos(canvasPos);
            ImGui.InvisibleButton("canvas_advanced", canvasSize);

            if (ImGui.BeginPopup("KfGraphContextMenu"))
            {
                var kf = track.Curve.Keys.FirstOrDefault(k => k.Id == contextKfId);
                if (kf != null)
                {
                    ImGui.Text("Interpolation Mode");
                    ImGui.Separator();
                    if (ImGui.MenuItem("Bezier", string.Empty, kf.Interpolation == InterpolationMode.Bezier)) kf.Interpolation = InterpolationMode.Bezier;
                    if (ImGui.MenuItem("Linear", string.Empty, kf.Interpolation == InterpolationMode.Linear)) kf.Interpolation = InterpolationMode.Linear;
                    if (ImGui.MenuItem("Constant", string.Empty, kf.Interpolation == InterpolationMode.Constant)) kf.Interpolation = InterpolationMode.Constant;
                }
                ImGui.EndPopup();
            }
        }

        private void HandleInput(Vector2 canvasPos, Vector2 canvasSize)
        {
            var io = ImGui.GetIO();
            bool isHovered = ImGui.IsMouseHoveringRect(canvasPos, canvasPos + canvasSize);

            if (ImGui.IsMouseReleased(ImGuiMouseButton.Left))
            {
                draggingKfId = Guid.Empty;
                draggingHandle = -1;
            }

            if (isHovered && ImGui.IsMouseClicked(ImGuiMouseButton.Middle)) isPanning = true;
            if (!ImGui.IsMouseDown(ImGuiMouseButton.Middle)) isPanning = false;

            if (isPanning)
            {
                pan.X -= io.MouseDelta.X / zoom.X;
                pan.Y += io.MouseDelta.Y / zoom.Y;
            }

            if (isHovered && io.MouseWheel != 0)
            {
                Vector2 mouseGraphBefore = ScreenToPoint(io.MousePos, canvasPos, canvasSize);
                
                float zoomFactor = io.MouseWheel > 0 ? 1.1f : 0.9f;

                if (io.KeyShift) 
                    zoom.X = Math.Clamp(zoom.X * zoomFactor, 1f, 1000f);
                else 
                    zoom.Y = Math.Clamp(zoom.Y * zoomFactor, 0.1f, 1000f);

                Vector2 mouseGraphAfter = ScreenToPoint(io.MousePos, canvasPos, canvasSize);
                pan += (mouseGraphBefore - mouseGraphAfter);
            }
        }

        private Vector2 PointToScreen(Vector2 point, Vector2 canvasPos, Vector2 canvasSize)
        {
            float x = canvasPos.X + (point.X - pan.X) * zoom.X;
            float y = canvasPos.Y + (canvasSize.Y / 2f) - (point.Y - pan.Y) * zoom.Y;
            return new Vector2(x, y);
        }

        private Vector2 ScreenToPoint(Vector2 screenPos, Vector2 canvasPos, Vector2 canvasSize)
        {
            float x = (screenPos.X - canvasPos.X) / zoom.X + pan.X;
            float y = (canvasPos.Y + (canvasSize.Y / 2f) - screenPos.Y) / zoom.Y + pan.Y;
            return new Vector2(x, y);
        }

        private void DrawGrid(ImDrawListPtr drawList, Vector2 canvasPos, Vector2 canvasSize)
        {
            uint axisCol = ImGui.GetColorU32(ImGuiCol.TextDisabled);
            uint gridCol = ImGui.GetColorU32(ImGuiCol.Border);

            Vector2 minGraph = ScreenToPoint(new Vector2(canvasPos.X, canvasPos.Y + canvasSize.Y), canvasPos, canvasSize);
            Vector2 maxGraph = ScreenToPoint(new Vector2(canvasPos.X + canvasSize.X, canvasPos.Y), canvasPos, canvasSize);

            float frameStep = zoom.X > 20 ? 1f : (zoom.X > 5 ? 5f : 10f);
            int startFrame = (int)(Math.Floor(minGraph.X / frameStep) * frameStep);
            int endFrame = (int)(Math.Ceiling(maxGraph.X / frameStep) * frameStep);

            for (int i = startFrame; i <= endFrame; i += (int)frameStep)
            {
                float x = PointToScreen(new Vector2(i, 0), canvasPos, canvasSize).X;
                drawList.AddLine(new Vector2(x, canvasPos.Y), new Vector2(x, canvasPos.Y + canvasSize.Y), gridCol, 1f);
                drawList.AddText(new Vector2(x + 4, canvasPos.Y + canvasSize.Y - 18), axisCol, i.ToString());
            }

            float valueStep = zoom.Y > 100 ? 0.1f : (zoom.Y > 20 ? 0.5f : 1f);
            float startVal = (float)(Math.Floor(minGraph.Y / valueStep) * valueStep);
            float endVal = (float)(Math.Ceiling(maxGraph.Y / valueStep) * valueStep);

            for (float v = startVal; v <= endVal; v += valueStep)
            {
                float y = PointToScreen(new Vector2(0, v), canvasPos, canvasSize).Y;
                drawList.AddLine(new Vector2(canvasPos.X, y), new Vector2(canvasPos.X + canvasSize.X, y), gridCol, 1f);
                drawList.AddText(new Vector2(canvasPos.X + 4, y - 16), axisCol, v.ToString("0.##"));
            }

            Vector2 zero = PointToScreen(Vector2.Zero, canvasPos, canvasSize);
            if (zero.X >= canvasPos.X && zero.X <= canvasPos.X + canvasSize.X)
                drawList.AddLine(new Vector2(zero.X, canvasPos.Y), new Vector2(zero.X, canvasPos.Y + canvasSize.Y), axisCol, 2f);
            if (zero.Y >= canvasPos.Y && zero.Y <= canvasPos.Y + canvasSize.Y)
                drawList.AddLine(new Vector2(canvasPos.X, zero.Y), new Vector2(canvasPos.X + canvasSize.X, zero.Y), axisCol, 2f);
        }

        private void DrawCurve(ImDrawListPtr drawList, AnimationCurve curve, uint color, Vector2 canvasPos, Vector2 canvasSize)
        {
            var keyframes = curve.Keys;
            if (keyframes.Count == 0) return;
            
            var io = ImGui.GetIO();
            var mousePos = io.MousePos;

            for (int i = 0; i < keyframes.Count - 1; i++)
            {
                var k1 = keyframes[i];
                var k2 = keyframes[i + 1];
                Vector2 p1Screen = PointToScreen(new Vector2(k1.Frame, k1.Value), canvasPos, canvasSize);
                Vector2 p2Screen = PointToScreen(new Vector2(k2.Frame, k2.Value), canvasPos, canvasSize);

                if (k1.Interpolation == InterpolationMode.Constant)
                {
                    drawList.AddLine(p1Screen, new Vector2(p2Screen.X, p1Screen.Y), color, 2.5f);
                    drawList.AddLine(new Vector2(p2Screen.X, p1Screen.Y), p2Screen, color, 2.5f);
                }
                else if (k1.Interpolation == InterpolationMode.Linear)
                {
                    drawList.AddLine(p1Screen, p2Screen, color, 2.5f);
                }
                else
                {
                    Vector2 cp1Screen = PointToScreen(new Vector2(k1.Frame, k1.Value) + k1.Tangents.Out, canvasPos, canvasSize);
                    Vector2 cp2Screen = PointToScreen(new Vector2(k2.Frame, k2.Value) + k2.Tangents.In, canvasPos, canvasSize);
                    drawList.AddBezierCubic(p1Screen, cp1Screen, cp2Screen, p2Screen, color, 2.5f, 0);
                }
            }

            for (int i = 0; i < keyframes.Count; i++)
            {
                var k = keyframes[i];
                Vector2 pGraph = new Vector2(k.Frame, k.Value);
                Vector2 pScreen = PointToScreen(pGraph, canvasPos, canvasSize);
                
                bool isSelected = selectedKfId == k.Id;
                uint ptColor = isSelected ? 0xFFFFFFFF : color;
                
                uint handleLineCol = isSelected ? 0xAAFFFFFF : ImGui.GetColorU32(ImGuiCol.TextDisabled);
                uint handlePtCol = isSelected ? 0xFFFFFFFF : 0xFFAAAAAA;

                if (k.Interpolation == InterpolationMode.Bezier)
                {
                    Vector2 cpInGraph = pGraph + k.Tangents.In;
                    Vector2 cpOutGraph = pGraph + k.Tangents.Out;
                    Vector2 cpInScreen = PointToScreen(cpInGraph, canvasPos, canvasSize);
                    Vector2 cpOutScreen = PointToScreen(cpOutGraph, canvasPos, canvasSize);

                    if (i > 0)
                    {
                        drawList.AddLine(pScreen, cpInScreen, handleLineCol, 1.5f);
                        drawList.AddCircleFilled(cpInScreen, 4f, handlePtCol);
                        
                        if (draggingHandle == -1 && Vector2.Distance(mousePos, cpInScreen) < 8f && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            selectedKfId = k.Id;
                            draggingKfId = k.Id;
                            draggingHandle = 2;
                        }
                    }

                    if (i < keyframes.Count - 1)
                    {
                        drawList.AddLine(pScreen, cpOutScreen, handleLineCol, 1.5f);
                        drawList.AddCircleFilled(cpOutScreen, 4f, handlePtCol);
                        
                        if (draggingHandle == -1 && Vector2.Distance(mousePos, cpOutScreen) < 8f && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                        {
                            selectedKfId = k.Id;
                            draggingKfId = k.Id;
                            draggingHandle = 1;
                        }
                    }

                    if (draggingKfId == k.Id && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                    {
                        Vector2 graphMouse = ScreenToPoint(mousePos, canvasPos, canvasSize);
                        var t = k.Tangents;

                        if (draggingHandle == 1)
                        {
                            float maxX = (i < keyframes.Count - 1)
                                ? (keyframes[i + 1].Frame - k.Frame)
                                : 9999f;

                            float dx = Math.Clamp(graphMouse.X - k.Frame, 0.001f, maxX);
                            float dy = graphMouse.Y - k.Value;

                            t.Out = new Vector2(dx, dy);

                            if (alignTangents && t.In.LengthSquared() > 0f)
                            {
                                Vector2 outScreen = new(dx * zoom.X, -dy * zoom.Y);

                                float inScreenLength = new Vector2(
                                    t.In.X * zoom.X,
                                    -t.In.Y * zoom.Y).Length();

                                Vector2 dir = Vector2.Normalize(outScreen);

                                Vector2 mirrored = -dir * inScreenLength;

                                t.In = new Vector2(
                                    mirrored.X / zoom.X,
                                    -mirrored.Y / zoom.Y);
                            }
                        }
                        else if (draggingHandle == 2)
                        {
                            float minX = (i > 0)
                                ? (keyframes[i - 1].Frame - k.Frame)
                                : -9999f;

                            float dx = Math.Clamp(graphMouse.X - k.Frame, minX, -0.001f);
                            float dy = graphMouse.Y - k.Value;

                            t.In = new Vector2(dx, dy);

                            if (alignTangents && t.Out.LengthSquared() > 0f)
                            {
                                Vector2 inScreen = new(dx * zoom.X, -dy * zoom.Y);

                                float outScreenLength = new Vector2(
                                    t.Out.X * zoom.X,
                                    -t.Out.Y * zoom.Y).Length();

                                Vector2 dir = Vector2.Normalize(inScreen);

                                Vector2 mirrored = -dir * outScreenLength;

                                t.Out = new Vector2(
                                    mirrored.X / zoom.X,
                                    -mirrored.Y / zoom.Y);
                            }
                        }

                        k.Tangents = t;
                    }
                }

                if (draggingHandle == -1 && Vector2.Distance(mousePos, pScreen) < 8f)
                {
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                    {
                        selectedKfId = k.Id;
                        draggingKfId = k.Id;
                        draggingHandle = 0;
                    }
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                    {
                        selectedKfId = k.Id;
                        contextKfId = k.Id;
                        ImGui.OpenPopup("KfGraphContextMenu");
                    }
                }

                if (draggingKfId == k.Id && draggingHandle == 0 && ImGui.IsMouseDown(ImGuiMouseButton.Left))
                {
                    Vector2 graphMouse = ScreenToPoint(mousePos, canvasPos, canvasSize);
                    k.Value = graphMouse.Y;
                    
                    int minFrame = i > 0 ? keyframes[i - 1].Frame + 1 : -999999;
                    int maxFrame = i < keyframes.Count - 1 ? keyframes[i + 1].Frame - 1 : 999999;
                    if (minFrame > maxFrame) minFrame = maxFrame;
                    
                    k.Frame = Math.Clamp((int)Math.Round(graphMouse.X), minFrame, maxFrame);
                    pScreen = PointToScreen(new Vector2(k.Frame, graphMouse.Y), canvasPos, canvasSize);
                }

                drawList.AddCircleFilled(pScreen, 5f, ptColor);
                drawList.AddCircle(pScreen, 5f, 0xFF000000, 0, 1.5f);
            }
        }
    }
}