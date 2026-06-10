using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;

namespace FanucProgrammer
{
    public enum CalligraphyMode
    {
        SingleStroke,    // Однолинейное письмо (центральная линия)
        OutlineFill,     // Обводка контуром
        BrushStroke      // Кистевое письмо с переменной толщиной
    }

    public static class SvgCalligraphyProcessor
    {
        public class CalligraphyStroke
        {
            public List<Point3D> Points { get; set; } = new List<Point3D>();
            public bool IsLiftMovement { get; set; } = false; // Подъем пера
            public double StrokeWidth { get; set; } = 1.0;
            public bool IsNewCharacter { get; set; } = false;
            public bool IsSingleStroke { get; set; } = false; // Для однолинейного письма
        }

        public static List<CalligraphyStroke> LoadSvgAsCalligraphyStrokes(
            string svgFilePath,
            double stepMm,
            double scale,
            Point3D offset,
            double liftHeight = 20.0, // Изменено: 10.0 → 20.0 по умолчанию
            CalligraphyMode mode = CalligraphyMode.SingleStroke,
            double strokeWidthVariation = 0.2)
        {
            var strokes = new List<CalligraphyStroke>();
            double pixelsToMm = 25.4 / 96.0;

            // Загружаем SVG
            var settings = new WpfDrawingSettings();
            var reader = new FileSvgReader(settings);
            var drawingGroup = reader.Read(svgFilePath);

            // Получаем все пути
            var paths = GetPaths(drawingGroup);

            if (mode == CalligraphyMode.SingleStroke)
            {
                // Для однолинейного письма используем специальную обработку
                return ProcessSingleStrokeMode(paths, stepMm, scale, offset, liftHeight, pixelsToMm);
            }
            else if (mode == CalligraphyMode.OutlineFill)
            {
                // Для обводки контуром
                return ProcessOutlineMode(paths, stepMm, scale, offset, liftHeight, pixelsToMm);
            }
            else if (mode == CalligraphyMode.BrushStroke)
            {
                // Для кистевого письма
                return ProcessBrushStrokeMode(paths, stepMm, scale, offset, liftHeight,
                                            strokeWidthVariation, pixelsToMm);
            }

            return strokes;
        }

        private static List<CalligraphyStroke> ProcessSingleStrokeMode(
            List<Path> paths, double stepMm, double scale, Point3D offset,
            double liftHeight, double pixelsToMm)
        {
            var strokes = new List<CalligraphyStroke>();

            // Убеждаемся, что высота подъема достаточна
            if (liftHeight < 5.0)
            {
                liftHeight = 20.0; // Минимальная безопасная высота
                Console.WriteLine($"WARNING: liftHeight слишком мал ({liftHeight}). Установлено 20.0 мм.");
            }

            foreach (var path in paths)
            {
                var geometry = path.Data.GetFlattenedPathGeometry();

                foreach (var figure in geometry.Figures)
                {
                    var points = ExtractFigurePoints(figure);

                    if (points.Count < 2)
                        continue;

                    // Убираем дублирующую последнюю точку для замкнутых контуров
                    if (points.Count > 2 && points[0] == points[points.Count - 1])
                    {
                        points.RemoveAt(points.Count - 1);
                    }

                    // Создаем штрих для каждой буквы/фигуры
                    var stroke = new CalligraphyStroke
                    {
                        IsSingleStroke = true,
                        StrokeWidth = 1.0,
                        IsLiftMovement = false,
                        IsNewCharacter = true // Это новая буква/символ
                    };

                    // Добавляем первую точку фигуры
                    AddPointToStroke(points[0], stroke, pixelsToMm, scale, offset);

                    // Добавляем остальные точки с интерполяцией
                    for (int i = 1; i < points.Count; i++)
                    {
                        var prevPoint = points[i - 1];
                        var nextPoint = points[i];

                        double segmentLength = Distance(prevPoint, nextPoint) * pixelsToMm;

                        if (segmentLength > stepMm)
                        {
                            int steps = Math.Max(1, (int)(segmentLength / stepMm));
                            double actualStep = segmentLength / steps;

                            for (int step = 1; step <= steps; step++)
                            {
                                double fraction = step / (double)steps;
                                Point interpolatedPoint = new Point(
                                    prevPoint.X + (nextPoint.X - prevPoint.X) * fraction,
                                    prevPoint.Y + (nextPoint.Y - prevPoint.Y) * fraction
                                );

                                AddPointToStroke(interpolatedPoint, stroke, pixelsToMm, scale, offset);
                            }
                        }

                        AddPointToStroke(nextPoint, stroke, pixelsToMm, scale, offset);
                    }

                    if (stroke.Points.Count > 0)
                    {
                        // Добавляем основной штрих рисования буквы
                        strokes.Add(stroke);

                        // Добавляем подъем после буквы - НЕ МЕНЕЕ 15 мм
                        var lastPoint = stroke.Points.Last();
                        var liftPoint = new Point3D(
                            lastPoint.X,
                            lastPoint.Y,
                            lastPoint.Z + Math.Max(liftHeight, 15.0));

                        var liftStroke = new CalligraphyStroke
                        {
                            IsLiftMovement = true,
                            Points = new List<Point3D> { lastPoint, liftPoint },
                            StrokeWidth = 1.0
                        };
                        strokes.Add(liftStroke);
                    }
                }
            }

            return strokes;
        }

        private static List<Point> CreateCenterLineFromContour(List<Point> contourPoints)
        {
            var centerLine = new List<Point>();

            if (contourPoints.Count < 3)
                return contourPoints; // Простой случай

            // Для замкнутых контуров вычисляем центральную точку
            bool isClosed = contourPoints.Count > 2 &&
                            contourPoints[0] == contourPoints[contourPoints.Count - 1];

            if (isClosed)
            {
                // Для замкнутого контура находим геометрический центр
                double centerX = 0, centerY = 0;
                for (int i = 0; i < contourPoints.Count - 1; i++) // Исключаем последнюю, если она дублирует первую
                {
                    centerX += contourPoints[i].X;
                    centerY += contourPoints[i].Y;
                }
                centerX /= (contourPoints.Count - 1);
                centerY /= (contourPoints.Count - 1);

                var center = new Point(centerX, centerY);

                // Создаем центральную линию как ломаную через центр и точки контура
                for (int i = 0; i < contourPoints.Count - 1; i++)
                {
                    // Точка на контуре
                    var contourPoint = contourPoints[i];

                    // Вычисляем точку на линии между контурной точкой и центром
                    var midPoint = new Point(
                        (contourPoint.X + center.X) / 2,
                        (contourPoint.Y + center.Y) / 2
                    );

                    centerLine.Add(midPoint);
                }

                // Замыкаем линию
                centerLine.Add(centerLine[0]);
            }
            else
            {
                // Для незамкнутого контура используем центральную линию как исходную
                // но с упрощением (убираем слишком близкие точки)
                centerLine.Add(contourPoints[0]);

                for (int i = 1; i < contourPoints.Count; i++)
                {
                    double distance = Distance(centerLine.Last(), contourPoints[i]);
                    if (distance > 0.5) // Порог для исключения слишком близких точек
                    {
                        centerLine.Add(contourPoints[i]);
                    }
                }
            }

            return centerLine;
        }

        // Вспомогательный метод для преобразования точки
        private static Point3D ApplyTransform(Point point, double pixelsToMm, double scale, Point3D offset)
        {
            double x = point.X * pixelsToMm * scale + offset.X;
            double y = point.Y * pixelsToMm * scale + offset.Y;
            y = -y; // Инвертируем Y
            return new Point3D(x, y, offset.Z);
        }

        // Вспомогательный метод для сравнения точек
        private static bool ArePointsEqual(Point3D p1, Point3D p2)
        {
            return Math.Abs(p1.X - p2.X) < 0.001 &&
                   Math.Abs(p1.Y - p2.Y) < 0.001 &&
                   Math.Abs(p1.Z - p2.Z) < 0.001;
        }

        private static List<CalligraphyStroke> ProcessOutlineMode(
            List<Path> paths, double stepMm, double scale, Point3D offset,
            double liftHeight, double pixelsToMm)
        {
            var strokes = new List<CalligraphyStroke>();

            // Убеждаемся, что высота подъема достаточна
            if (liftHeight < 5.0)
            {
                liftHeight = 20.0; // Минимальная безопасная высота
                Console.WriteLine($"WARNING: liftHeight слишком мал ({liftHeight}). Установлено 20.0 мм.");
            }

            foreach (var path in paths)
            {
                var geometry = path.Data.GetFlattenedPathGeometry();

                foreach (var figure in geometry.Figures)
                {
                    var points = ExtractFigurePoints(figure);

                    if (points.Count < 2)
                        continue;

                    // Убираем дублирующую последнюю точку для замкнутых контуров
                    if (points.Count > 2 && points[0] == points[points.Count - 1])
                    {
                        points.RemoveAt(points.Count - 1);
                    }

                    var stroke = new CalligraphyStroke
                    {
                        StrokeWidth = 1.0,
                        IsLiftMovement = false,
                        IsNewCharacter = true
                    };

                    // Добавляем все точки контура с интерполяцией
                    for (int i = 0; i < points.Count; i++)
                    {
                        // Добавляем текущую точку
                        AddPointToStroke(points[i], stroke, pixelsToMm, scale, offset);

                        // Добавляем промежуточные точки между точками контура
                        if (i < points.Count - 1)
                        {
                            var prevPoint = points[i];
                            var nextPoint = points[i + 1];

                            double segmentLength = Distance(prevPoint, nextPoint) * pixelsToMm;

                            if (segmentLength > stepMm)
                            {
                                int steps = Math.Max(1, (int)(segmentLength / stepMm));
                                double actualStep = segmentLength / steps;

                                for (int step = 1; step < steps; step++)
                                {
                                    double fraction = step / (double)steps;
                                    Point interpolatedPoint = new Point(
                                        prevPoint.X + (nextPoint.X - prevPoint.X) * fraction,
                                        prevPoint.Y + (nextPoint.Y - prevPoint.Y) * fraction
                                    );

                                    AddPointToStroke(interpolatedPoint, stroke, pixelsToMm, scale, offset);
                                }
                            }
                        }
                    }

                    if (stroke.Points.Count > 0)
                    {
                        // Добавляем штрих контура
                        strokes.Add(stroke);

                        // Добавляем подъем после контура - НЕ МЕНЕЕ 15 мм
                        var lastPoint = stroke.Points.Last();
                        var liftPoint = new Point3D(
                            lastPoint.X,
                            lastPoint.Y,
                            lastPoint.Z + Math.Max(liftHeight, 15.0));

                        var liftStroke = new CalligraphyStroke
                        {
                            IsLiftMovement = true,
                            Points = new List<Point3D> { lastPoint, liftPoint },
                            StrokeWidth = 1.0
                        };
                        strokes.Add(liftStroke);
                    }
                }
            }

            return strokes;
        }

        private static List<CalligraphyStroke> ProcessBrushStrokeMode(
            List<Path> paths, double stepMm, double scale, Point3D offset,
            double liftHeight, double strokeWidthVariation, double pixelsToMm)
        {
            var strokes = new List<CalligraphyStroke>();
            Random rand = new Random(DateTime.Now.Millisecond);

            CalligraphyStroke previousStroke = null;

            // Убеждаемся, что высота подъема достаточна
            if (liftHeight < 5.0)
            {
                liftHeight = 20.0; // Минимальная безопасная высота
                Console.WriteLine($"WARNING: liftHeight слишком мал ({liftHeight}). Установлено 20.0 мм.");
            }

            foreach (var path in paths)
            {
                var geometry = path.Data.GetFlattenedPathGeometry();

                foreach (var figure in geometry.Figures)
                {
                    // Добавляем подъем перед новой фигурой (буквой)
                    if (previousStroke != null && previousStroke.Points.Count > 0)
                    {
                        var lastPointOfPrev = previousStroke.Points.Last();
                        var liftPoint = new Point3D(
                            lastPointOfPrev.X,
                            lastPointOfPrev.Y,
                            lastPointOfPrev.Z + Math.Max(liftHeight, 15.0));

                        var liftStroke = new CalligraphyStroke
                        {
                            IsLiftMovement = true,
                            Points = new List<Point3D> { lastPointOfPrev, liftPoint },
                            StrokeWidth = 1.0,
                            IsNewCharacter = true
                        };
                        strokes.Add(liftStroke);
                    }

                    var points = ExtractFigurePoints(figure);

                    if (points.Count < 2)
                        continue;

                    // Убираем дублирующую последнюю точку для замкнутых контуров
                    if (points.Count > 2 && points[0] == points[points.Count - 1])
                    {
                        points.RemoveAt(points.Count - 1);
                    }

                    var stroke = new CalligraphyStroke
                    {
                        StrokeWidth = 1.0 + (rand.NextDouble() - 0.5) * strokeWidthVariation,
                        IsLiftMovement = false,
                        IsNewCharacter = true
                    };

                    // Добавляем все точки с вариацией ширины
                    for (int i = 0; i < points.Count; i++)
                    {
                        // Добавляем текущую точку
                        AddPointToStroke(points[i], stroke, pixelsToMm, scale, offset);

                        // Изменяем ширину штриха в зависимости от направления
                        if (i > 0 && i < points.Count - 1)
                        {
                            Vector direction = points[i] - points[i - 1];
                            double angle = Math.Atan2(direction.Y, direction.X) * 180 / Math.PI;

                            // Синусоидальное изменение ширины
                            double pressureVariation = Math.Sin(angle * Math.PI / 180) * strokeWidthVariation;
                            stroke.StrokeWidth = Math.Max(0.5, 1.0 + pressureVariation);

                            // Добавляем случайность
                            stroke.StrokeWidth += (rand.NextDouble() - 0.5) * strokeWidthVariation * 0.3;
                        }

                        // Добавляем промежуточные точки
                        if (i < points.Count - 1)
                        {
                            var prevPoint = points[i];
                            var nextPoint = points[i + 1];

                            double segmentLength = Distance(prevPoint, nextPoint) * pixelsToMm;

                            if (segmentLength > stepMm)
                            {
                                int steps = Math.Max(1, (int)(segmentLength / stepMm));
                                double actualStep = segmentLength / steps;

                                for (int step = 1; step < steps; step++)
                                {
                                    double fraction = step / (double)steps;
                                    Point interpolatedPoint = new Point(
                                        prevPoint.X + (nextPoint.X - prevPoint.X) * fraction,
                                        prevPoint.Y + (nextPoint.Y - prevPoint.Y) * fraction
                                    );

                                    AddPointToStroke(interpolatedPoint, stroke, pixelsToMm, scale, offset);

                                    // Небольшое изменение ширины во время движения
                                    stroke.StrokeWidth += (rand.NextDouble() - 0.5) * strokeWidthVariation * 0.1;
                                    stroke.StrokeWidth = Math.Max(0.5, Math.Min(2.0, stroke.StrokeWidth));
                                }
                            }
                        }
                    }

                    if (stroke.Points.Count > 0)
                    {
                        strokes.Add(stroke);

                        // Добавляем подъем после штриха - НЕ МЕНЕЕ 15 мм
                        var lastPoint = stroke.Points.Last();
                        var liftPoint = new Point3D(
                            lastPoint.X,
                            lastPoint.Y,
                            lastPoint.Z + Math.Max(liftHeight * (1 + stroke.StrokeWidth * 0.1), 15.0));

                        var liftStroke = new CalligraphyStroke
                        {
                            IsLiftMovement = true,
                            Points = new List<Point3D> { lastPoint, liftPoint },
                            StrokeWidth = 1.0
                        };
                        strokes.Add(liftStroke);
                    }

                    if (stroke != null && stroke.Points.Count > 0)
                    {
                        previousStroke = stroke;
                    }
                }
            }

            return strokes;
        }

        private static List<Point> SimplifyToCenterLine(List<Point> contourPoints)
        {
            var centerLine = new List<Point>();

            if (contourPoints.Count < 4)
                return contourPoints; // Недостаточно точек для упрощения

            // Простой алгоритм: берем средние точки между противоположными сторонами контура
            int halfCount = contourPoints.Count / 2;

            for (int i = 0; i < halfCount; i++)
            {
                Point p1 = contourPoints[i];
                Point p2 = contourPoints[i + halfCount];

                // Средняя точка
                Point center = new Point(
                    (p1.X + p2.X) / 2,
                    (p1.Y + p2.Y) / 2
                );

                centerLine.Add(center);
            }

            // Добавляем первую точку в конец для замкнутой линии
            if (centerLine.Count > 0)
                centerLine.Add(centerLine[0]);

            return centerLine;
        }

        private static List<Point> ExtractFigurePoints(PathFigure figure)
        {
            var points = new List<Point>();
            points.Add(figure.StartPoint);

            foreach (var segment in figure.Segments)
            {
                if (segment is LineSegment lineSegment)
                {
                    points.Add(lineSegment.Point);
                }
                else if (segment is PolyLineSegment polyLineSegment)
                {
                    points.AddRange(polyLineSegment.Points);
                }
                else if (segment is BezierSegment bezierSegment)
                {
                    // Аппроксимируем кривую Безье линейными сегментами
                    var bezierPoints = ApproximateBezier(
                        points.Last(),
                        bezierSegment.Point1,
                        bezierSegment.Point2,
                        bezierSegment.Point3,
                        10
                    );
                    points.AddRange(bezierPoints);
                }
                else if (segment is PolyBezierSegment polyBezierSegment)
                {
                    var currentPoint = points.Last();
                    for (int i = 0; i < polyBezierSegment.Points.Count; i += 3)
                    {
                        if (i + 2 < polyBezierSegment.Points.Count)
                        {
                            var bezierPoints = ApproximateBezier(
                                currentPoint,
                                polyBezierSegment.Points[i],
                                polyBezierSegment.Points[i + 1],
                                polyBezierSegment.Points[i + 2],
                                10
                            );
                            points.AddRange(bezierPoints);
                            currentPoint = polyBezierSegment.Points[i + 2];
                        }
                    }
                }
                else if (segment is QuadraticBezierSegment quadraticSegment)
                {
                    // Квадратичные кривые Безье
                    var bezierPoints = ApproximateQuadraticBezier(
                        points.Last(),
                        quadraticSegment.Point1,
                        quadraticSegment.Point2,
                        10
                    );
                    points.AddRange(bezierPoints);
                }
            }

            return points;
        }

        private static List<Point> ApproximateBezier(Point p0, Point p1, Point p2, Point p3, int segments)
        {
            var points = new List<Point>();
            for (int i = 1; i <= segments; i++)
            {
                double t = i / (double)segments;
                double u = 1 - t;
                double tt = t * t;
                double uu = u * u;
                double uuu = uu * u;
                double ttt = tt * t;

                Point point = new Point(
                    uuu * p0.X + 3 * uu * t * p1.X + 3 * u * tt * p2.X + ttt * p3.X,
                    uuu * p0.Y + 3 * uu * t * p1.Y + 3 * u * tt * p2.Y + ttt * p3.Y
                );
                points.Add(point);
            }
            return points;
        }

        private static List<Point> ApproximateQuadraticBezier(Point p0, Point p1, Point p2, int segments)
        {
            var points = new List<Point>();
            for (int i = 1; i <= segments; i++)
            {
                double t = i / (double)segments;
                double u = 1 - t;

                Point point = new Point(
                    u * u * p0.X + 2 * u * t * p1.X + t * t * p2.X,
                    u * u * p0.Y + 2 * u * t * p1.Y + t * t * p2.Y
                );
                points.Add(point);
            }
            return points;
        }

        private static void AddPointToStroke(Point point, CalligraphyStroke stroke,
                                           double pixelsToMm, double scale, Point3D offset)
        {
            double x = point.X * pixelsToMm * scale + offset.X;
            double y = point.Y * pixelsToMm * scale + offset.Y;
            y = -y; // Инвертируем Y

            stroke.Points.Add(new Point3D(x, y, offset.Z));
        }

        private static double CalculateAngle(Point p1, Point p2, Point p3)
        {
            Vector v1 = p1 - p2;
            Vector v2 = p3 - p2;

            double dot = v1.X * v2.X + v1.Y * v2.Y;
            double mag1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
            double mag2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);

            if (mag1 == 0 || mag2 == 0)
                return 180;

            double cosAngle = dot / (mag1 * mag2);
            cosAngle = Math.Max(-1, Math.Min(1, cosAngle));

            return Math.Acos(cosAngle) * 180 / Math.PI;
        }


        public static double CalculateRFromStrokeWidth(double strokeWidth)
        {
            // Преобразуем ширину штриха (0.1-2.0) в угол R (0-90 градусов)
            // 0.1 = R = 0° (вертикально вниз)
            // 1.0 = R = 45° (наклонно)
            // 2.0 = R = 90° (горизонтально вправо)

            strokeWidth = Math.Max(0.1, Math.Min(2.0, strokeWidth));
            double normalized = (strokeWidth - 0.1) / (2.0 - 0.1);
            return normalized * 90.0; // 0-90 градусов
        }

        private static double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        // Метод для получения всех Path из DrawingGroup
        private static List<Path> GetPaths(DrawingGroup drawingGroup)
        {
            var paths = new List<Path>();
            GetPathsFromDrawing(drawingGroup, paths, null);
            return paths;
        }

        private static void GetPathsFromDrawing(Drawing drawing, List<Path> paths, Transform currentTransform)
        {
            if (drawing is DrawingGroup group)
            {
                Transform newTransform = currentTransform;

                if (group.Transform != null)
                {
                    if (currentTransform != null)
                    {
                        var transformGroup = new TransformGroup();
                        transformGroup.Children.Add(currentTransform);
                        transformGroup.Children.Add(group.Transform);
                        newTransform = transformGroup;
                    }
                    else
                    {
                        newTransform = group.Transform;
                    }
                }

                foreach (var child in group.Children)
                {
                    GetPathsFromDrawing(child, paths, newTransform);
                }
            }
            else if (drawing is GeometryDrawing geometryDrawing)
            {
                var geometry = geometryDrawing.Geometry.Clone();
                Transform geometryTransform = geometry.Transform;
                Transform finalTransform = currentTransform;

                if (geometryTransform != null)
                {
                    if (finalTransform != null)
                    {
                        var transformGroup = new TransformGroup();
                        transformGroup.Children.Add(finalTransform);
                        transformGroup.Children.Add(geometryTransform);
                        finalTransform = transformGroup;
                    }
                    else
                    {
                        finalTransform = geometryTransform;
                    }
                }

                if (finalTransform != null)
                {
                    geometry.Transform = finalTransform;
                }

                var flattenedGeometry = geometry.GetFlattenedPathGeometry();
                var path = new Path
                {
                    Data = flattenedGeometry
                };
                paths.Add(path);
            }
        }
    }
}