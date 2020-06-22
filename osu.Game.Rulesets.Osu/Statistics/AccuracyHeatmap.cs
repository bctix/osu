// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Scoring;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Statistics
{
    public class AccuracyHeatmap : CompositeDrawable
    {
        /// <summary>
        /// Size of the inner circle containing the "hit" points, relative to the size of this <see cref="AccuracyHeatmap"/>.
        /// All other points outside of the inner circle are "miss" points.
        /// </summary>
        private const float inner_portion = 0.8f;

        /// <summary>
        /// Number of rows/columns of points.
        /// 4px per point @ 128x128 size (the contents of the <see cref="AccuracyHeatmap"/> are always square). 1024 total points.
        /// </summary>
        private const int points_per_dimension = 32;

        private const float rotation = 45;

        private GridContainer pointGrid;

        private readonly ScoreInfo score;
        private readonly IBeatmap playableBeatmap;

        public AccuracyHeatmap(ScoreInfo score, IBeatmap playableBeatmap)
        {
            this.score = score;
            this.playableBeatmap = playableBeatmap;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                Children = new Drawable[]
                {
                    new CircularContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                        Size = new Vector2(inner_portion),
                        Masking = true,
                        BorderThickness = 2f,
                        BorderColour = Color4.White,
                        Child = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4Extensions.FromHex("#202624")
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Masking = true,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Y,
                                Height = 2, // We're rotating along a diagonal - we don't really care how big this is.
                                Width = 1f,
                                Rotation = -rotation,
                                Alpha = 0.3f,
                            },
                            new Box
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                RelativeSizeAxes = Axes.Y,
                                Height = 2, // We're rotating along a diagonal - we don't really care how big this is.
                                Width = 1f,
                                Rotation = rotation
                            },
                            new Box
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Width = 10,
                                Height = 2f,
                            },
                            new Box
                            {
                                Anchor = Anchor.TopRight,
                                Origin = Anchor.TopRight,
                                Y = -1,
                                Width = 2f,
                                Height = 10,
                            }
                        }
                    },
                    pointGrid = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both
                    }
                }
            };

            Vector2 centre = new Vector2(points_per_dimension) / 2;
            float innerRadius = centre.X * inner_portion;

            Drawable[][] points = new Drawable[points_per_dimension][];

            for (int r = 0; r < points_per_dimension; r++)
            {
                points[r] = new Drawable[points_per_dimension];

                for (int c = 0; c < points_per_dimension; c++)
                {
                    HitPointType pointType = Vector2.Distance(new Vector2(c, r), centre) <= innerRadius
                        ? HitPointType.Hit
                        : HitPointType.Miss;

                    var point = new HitPoint(pointType)
                    {
                        Colour = pointType == HitPointType.Hit ? new Color4(102, 255, 204, 255) : new Color4(255, 102, 102, 255)
                    };

                    points[r][c] = point;
                }
            }

            pointGrid.Content = points;

            if (score.HitEvents == null || score.HitEvents.Count == 0)
                return;

            // Todo: This should probably not be done like this.
            float radius = OsuHitObject.OBJECT_RADIUS * (1.0f - 0.7f * (playableBeatmap.BeatmapInfo.BaseDifficulty.CircleSize - 5) / 5) / 2;

            foreach (var e in score.HitEvents.Where(e => e.HitObject is HitCircle))
            {
                if (e.LastHitObject == null || e.PositionOffset == null)
                    continue;

                AddPoint(((OsuHitObject)e.LastHitObject).StackedEndPosition, ((OsuHitObject)e.HitObject).StackedEndPosition, e.PositionOffset.Value, radius);
            }
        }

        protected void AddPoint(Vector2 start, Vector2 end, Vector2 hitPoint, float radius)
        {
            if (pointGrid.Content.Length == 0)
                return;

            double angle1 = Math.Atan2(end.Y - hitPoint.Y, hitPoint.X - end.X); // Angle between the end point and the hit point.
            double angle2 = Math.Atan2(end.Y - start.Y, start.X - end.X); // Angle between the end point and the start point.
            double finalAngle = angle2 - angle1; // Angle between start, end, and hit points.
            float normalisedDistance = Vector2.Distance(hitPoint, end) / radius;

            // Consider two objects placed horizontally, with the start on the left and the end on the right.
            // The above calculated the angle between {end, start}, and the angle between {end, hitPoint}, in the form:
            //             +pi | 0
            //     O --------- O ----->      Note: Math.Atan2 has a range (-pi <= theta <= +pi)
            //             -pi | 0
            // E.g. If the hit point was directly above end, it would have an angle pi/2.
            //
            // It also calculated the angle separating hitPoint from the line joining {start, end}, that is anti-clockwise in the form:
            //               0 | pi
            //     O --------- O ----->
            //             2pi | pi
            //
            // However keep in mind that cos(0)=1 and cos(2pi)=1, whereas we actually want these values to appear on the left, so the x-coordinate needs to be inverted.
            // Likewise sin(pi/2)=1 and sin(3pi/2)=-1, whereas we actually want these values to appear on the bottom/top respectively, so the y-coordinate also needs to be inverted.
            //
            // We also need to apply the anti-clockwise rotation.
            var rotatedAngle = finalAngle - MathUtils.DegreesToRadians(rotation);
            var rotatedCoordinate = -1 * new Vector2((float)Math.Cos(rotatedAngle), (float)Math.Sin(rotatedAngle));

            Vector2 localCentre = new Vector2(points_per_dimension) / 2;
            float localRadius = localCentre.X * inner_portion * normalisedDistance; // The radius inside the inner portion which of the heatmap which the closest point lies.
            Vector2 localPoint = localCentre + localRadius * rotatedCoordinate;

            // Find the most relevant hit point.
            int r = Math.Clamp((int)Math.Round(localPoint.Y), 0, points_per_dimension - 1);
            int c = Math.Clamp((int)Math.Round(localPoint.X), 0, points_per_dimension - 1);

            ((HitPoint)pointGrid.Content[r][c]).Increment();
        }

        private class HitPoint : Circle
        {
            private readonly HitPointType pointType;

            public HitPoint(HitPointType pointType)
            {
                this.pointType = pointType;

                RelativeSizeAxes = Axes.Both;
                Alpha = 0;
            }

            public void Increment()
            {
                if (Alpha < 1)
                    Alpha += 0.1f;
                else if (pointType == HitPointType.Hit)
                    Colour = ((Color4)Colour).Lighten(0.1f);
            }
        }

        private enum HitPointType
        {
            Hit,
            Miss
        }
    }
}
