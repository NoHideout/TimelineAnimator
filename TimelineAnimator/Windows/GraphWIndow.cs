using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Bindings.ImPlot;
using Dalamud.Interface.Windowing;
using TimelineAnimator.Data;
using TimelineAnimator.Sequencers;

//Todo actually do this properly rather than quick thing

namespace TimelineAnimator.Windows
{
    public class GraphEditorWindow : Window, IDisposable
    {
        private Guid contextKfId = Guid.Empty;
        private Guid selectedKfId = Guid.Empty;
        private bool alignTangents = true;

        public GraphEditorWindow() : base("Graph Editor###GraphEditorWindow")
        {
            Size = new Vector2(600, 400);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose()
        {
        }

        public override void Draw()
        {
            var workspace = Services.WorkspaceService;
            var seq = workspace.GetActiveSequencer() as SequencerBase;
            if (seq == null || workspace.SharedSelectedEntry < 0)
            {
                ImGui.TextDisabled("Select a specific property track to view its graph.");
                return;
            }

            var rows = seq.GetFlattenedRows();
            if (workspace.SharedSelectedEntry >= rows.Count) return;

            var row = rows[workspace.SharedSelectedEntry];
            if (row.PropTrack == null)
            {
                ImGui.TextDisabled("Select a specific property track to view its graph.");
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
                ImPlot.SetNextAxesToFit();
            }

            var canvasSize = ImGui.GetContentRegionAvail();
            if (canvasSize.X < 50 || canvasSize.Y < 50) return;

            bool openContextMenu = false;

            if (ImPlot.BeginPlot("##CurvePlot", canvasSize, ImPlotFlags.NoTitle | ImPlotFlags.NoLegend))
            {
                ImPlot.SetupAxes("Frame", "");

                double playheadPos = Services.PlaybackService.CurrentFrame;
                if (ImPlot.DragLineX(99999, ref playheadPos, new Vector4(1f, 0.2f, 0.2f, 1f), 1.5f))
                {
                    int newFrame = Math.Clamp((int)Math.Round(playheadPos), 0, seq.Clip.EndFrame);

                    if (newFrame != Services.PlaybackService.CurrentFrame)
                    {
                        Services.PlaybackService.CurrentFrame = newFrame;
                        seq.ApplyPose(newFrame);
                    }
                }

                var keyframes = track.Curve.Keys;
                if (keyframes.Count > 0)
                {
                    DrawPlotCurve(track.Curve);
                    HandleDragPoints(track.Curve, seq);

                    openContextMenu = HandleContextMenu(track.Curve);
                }

                ImPlot.EndPlot();
            }

            if (openContextMenu) ImGui.OpenPopup("KfGraphContextMenu");
            DrawContextMenuPopup(track, seq);
        }

        private void DrawPlotCurve(AnimationCurve curve)
        {
            var keys = curve.Keys;
            if (keys.Count < 2) return;

            ImPlot.PushStyleColor(ImPlotCol.Line, new Vector4(0f, 1f, 0f, 1f));
            ImPlot.PushStyleVar(ImPlotStyleVar.LineWeight, 2.5f);

            for (int i = 0; i < keys.Count - 1; i++)
            {
                var k1 = keys[i];
                var k2 = keys[i + 1];

                if (k1.Interpolation == InterpolationMode.Constant)
                {
                    double[] xs = { k1.Frame, k2.Frame, k2.Frame };
                    double[] ys = { k1.Value, k1.Value, k2.Value };
                    ImPlot.PlotLine($"##curve_{i}", ref xs[0], ref ys[0], 3);
                }
                else if (k1.Interpolation == InterpolationMode.Linear)
                {
                    double[] xs = { k1.Frame, k2.Frame };
                    double[] ys = { k1.Value, k2.Value };
                    ImPlot.PlotLine($"##curve_{i}", ref xs[0], ref ys[0], 2);
                }
                else
                {
                    int samples = 100;
                    double[] xs = new double[samples];
                    double[] ys = new double[samples];

                    Vector2 p0 = new Vector2(k1.Frame, k1.Value);
                    Vector2 p1 = p0 + k1.Tangents.Out;
                    Vector2 p2 = new Vector2(k2.Frame, k2.Value) + k2.Tangents.In;
                    Vector2 p3 = new Vector2(k2.Frame, k2.Value);

                    for (int s = 0; s < samples; s++)
                    {
                        double t = s / (double)(samples - 1);
                        double u = 1.0 - t;

                        double w1 = u * u * u;
                        double w2 = 3 * u * u * t;
                        double w3 = 3 * u * t * t;
                        double w4 = t * t * t;

                        xs[s] = w1 * p0.X + w2 * p1.X + w3 * p2.X + w4 * p3.X;
                        ys[s] = w1 * p0.Y + w2 * p1.Y + w3 * p2.Y + w4 * p3.Y;
                    }

                    ImPlot.PlotLine($"##curve_{i}", ref xs[0], ref ys[0], samples);
                }
            }

            ImPlot.PopStyleVar();
            ImPlot.PopStyleColor();
        }

        private void HandleDragPoints(AnimationCurve curve, SequencerBase seq)
        {
            var keys = curve.Keys;
            Vector4 colorUnselected = new Vector4(0f, 1f, 0f, 1f);
            Vector4 colorSelected = new Vector4(1f, 1f, 1f, 1f);
            Vector4 colorHandle = new Vector4(0.7f, 0.7f, 0.7f, 1f);
            Vector4 colorHandleLine = new Vector4(0.5f, 0.5f, 0.5f, 0.8f);

            bool needsUpdate = false;

            if (ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                var kf = GetKeyframeUnderMouse(curve);
                if (kf != null)
                    selectedKfId = kf.Id;
            }

            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                bool isSelected = selectedKfId == k.Id;

                double kx = k.Frame;
                double ky = k.Value;

                if (k.Interpolation == InterpolationMode.Bezier && i < keys.Count - 1)
                {
                    double outX = kx + k.Tangents.Out.X;
                    double outY = ky + k.Tangents.Out.Y;

                    double[] lineX = { kx, outX };
                    double[] lineY = { ky, outY };
                    ImPlot.SetNextLineStyle(colorHandleLine, 1.5f);
                    ImPlot.PlotLine($"##outL{i}", ref lineX[0], ref lineY[0], 2);

                    if (ImPlot.DragPoint(i * 10 + 1, ref outX, ref outY, colorHandle, 4))
                    {
                        selectedKfId = k.Id;

                        float maxX = (float)(keys[i + 1].Frame - k.Frame);
                        float dx = Math.Clamp((float)(outX - kx), 0.001f, maxX);
                        float dy = (float)(outY - ky);

                        var tangents = k.Tangents;
                        tangents.Out = new Vector2(dx, dy);

                        if (alignTangents && tangents.In.LengthSquared() > 0f)
                        {
                            Vector2 pCenter = ImPlot.PlotToPixels(new ImPlotPoint(kx, ky));
                            Vector2 pOut = ImPlot.PlotToPixels(new ImPlotPoint(kx + dx, ky + dy));
                            Vector2 pIn = ImPlot.PlotToPixels(new ImPlotPoint(kx + tangents.In.X, ky + tangents.In.Y));

                            Vector2 dirOut = Vector2.Normalize(pOut - pCenter);
                            float lenIn = (pIn - pCenter).Length();

                            Vector2 newPIn = pCenter - (dirOut * lenIn);
                            ImPlotPoint mirroredGraph = ImPlot.PixelsToPlot(newPIn);

                            tangents.In = new Vector2((float)(mirroredGraph.X - kx), (float)(mirroredGraph.Y - ky));
                        }

                        k.Tangents = tangents;
                        needsUpdate = true;
                    }
                }

                if (k.Interpolation == InterpolationMode.Bezier && i > 0)
                {
                    double inX = kx + k.Tangents.In.X;
                    double inY = ky + k.Tangents.In.Y;

                    double[] lineX = { kx, inX };
                    double[] lineY = { ky, inY };
                    ImPlot.SetNextLineStyle(colorHandleLine, 1.5f);
                    ImPlot.PlotLine($"##inL{i}", ref lineX[0], ref lineY[0], 2);

                    if (ImPlot.DragPoint(i * 10 + 2, ref inX, ref inY, colorHandle, 4))
                    {
                        selectedKfId = k.Id;

                        float minX = (float)(keys[i - 1].Frame - k.Frame);
                        float dx = Math.Clamp((float)(inX - kx), minX, -0.001f);
                        float dy = (float)(inY - ky);

                        var tangents = k.Tangents;
                        tangents.In = new Vector2(dx, dy);

                        if (alignTangents && tangents.Out.LengthSquared() > 0f)
                        {
                            Vector2 pCenter = ImPlot.PlotToPixels(new ImPlotPoint(kx, ky));
                            Vector2 pIn = ImPlot.PlotToPixels(new ImPlotPoint(kx + dx, ky + dy));
                            Vector2 pOut =
                                ImPlot.PlotToPixels(new ImPlotPoint(kx + tangents.Out.X, ky + tangents.Out.Y));

                            Vector2 dirIn = Vector2.Normalize(pIn - pCenter);
                            float lenOut = (pOut - pCenter).Length();

                            Vector2 newPOut = pCenter - (dirIn * lenOut);
                            ImPlotPoint mirroredGraph = ImPlot.PixelsToPlot(newPOut);

                            tangents.Out = new Vector2((float)(mirroredGraph.X - kx), (float)(mirroredGraph.Y - ky));
                        }

                        k.Tangents = tangents;
                        needsUpdate = true;
                    }
                }

                if (ImPlot.DragPoint(i * 10, ref kx, ref ky, isSelected ? colorSelected : colorUnselected, 5))
                {
                    selectedKfId = k.Id;

                    int minFrame = i > 0 ? keys[i - 1].Frame + 1 : -999999;
                    int maxFrame = i < keys.Count - 1 ? keys[i + 1].Frame - 1 : 999999;
                    if (minFrame > maxFrame) minFrame = maxFrame;

                    k.Frame = Math.Clamp((int)Math.Round(kx), minFrame, maxFrame);
                    k.Value = (float)ky;
                    needsUpdate = true;
                }
            }

            if (needsUpdate)
            {
                seq.ApplyPose(Services.PlaybackService.CurrentFrame);
            }
        }

        private bool HandleContextMenu(AnimationCurve curve)
        {
            if (!ImGui.IsMouseReleased(ImGuiMouseButton.Right))
                return false;

            var kf = GetKeyframeUnderMouse(curve);
            if (kf == null)
                return false;

            selectedKfId = kf.Id;
            contextKfId = kf.Id;

            return true;
        }

        private void DrawContextMenuPopup(PropertyTrack track, SequencerBase seq)
        {
            if (ImGui.BeginPopup("KfGraphContextMenu"))
            {
                var kf = track.Curve.Keys.FirstOrDefault(k => k.Id == contextKfId);
                if (kf != null)
                {
                    ImGui.Text("Interpolation Mode");
                    ImGui.Separator();

                    bool changed = false;
                    if (ImGui.MenuItem("Bezier", string.Empty, kf.Interpolation == InterpolationMode.Bezier))
                    {
                        kf.Interpolation = InterpolationMode.Bezier;
                        changed = true;
                    }

                    if (ImGui.MenuItem("Linear", string.Empty, kf.Interpolation == InterpolationMode.Linear))
                    {
                        kf.Interpolation = InterpolationMode.Linear;
                        changed = true;
                    }

                    if (ImGui.MenuItem("Constant", string.Empty, kf.Interpolation == InterpolationMode.Constant))
                    {
                        kf.Interpolation = InterpolationMode.Constant;
                        changed = true;
                    }

                    if (changed)
                    {
                        seq.ApplyPose(Services.PlaybackService.CurrentFrame);
                    }
                }

                ImGui.EndPopup();
            }
        }

        private CurveKeyframe? GetKeyframeUnderMouse(AnimationCurve curve, float maxDistanceSquared = 500f)
        {
            var mouse = ImGui.GetMousePos();

            CurveKeyframe? closest = null;
            float closestDist = maxDistanceSquared;

            foreach (var k in curve.Keys)
            {
                var pos = ImPlot.PlotToPixels(new ImPlotPoint(k.Frame, k.Value));
                float dist = Vector2.DistanceSquared(mouse, pos);

                if (dist < closestDist)
                {
                    closest = k;
                    closestDist = dist;
                }
            }

            return closest;
        }
    }
}