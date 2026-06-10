using SharpVectors.Converters;
using SharpVectors.Renderers.Wpf;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

// Алиасы для разрешения конфликта имен
using IO_Path = System.IO.Path;
using Shapes_Path = System.Windows.Shapes.Path;

namespace FanucProgrammer
{
    public static class SvgToRobotPath
    {
        public static List<Point3D> LoadSvgAs3DPoints(string svgFilePath, double stepMm, double scale, Point3D offset)
        {
            var points = new List<Point3D>();
            double pixelsToMm = 25.4 / 96.0;

            // 1. Создаём объект настроек рендеринга
            var settings = new WpfDrawingSettings();

            // 2. Создаём FileSvgReader, передавая настройки в конструктор
            var reader = new FileSvgReader(settings);

            // 3. Загружаем SVG файл, вызывая метод Read()
            var drawingGroup = reader.Read(svgFilePath);

            // Получаем все пути из SVG
            var paths = GetPaths(drawingGroup);

            foreach (var path in paths)
            {
                // Получаем и сглаживаем геометрию пути
                var geometry = path.Data.GetFlattenedPathGeometry();

                // Собираем все точки из сглаженной геометрии
                var pointCollection = new List<Point>();

                foreach (var figure in geometry.Figures)
                {
                    Point currentPoint = figure.StartPoint;
                    pointCollection.Add(currentPoint);

                    foreach (var segment in figure.Segments)
                    {
                        if (segment is LineSegment lineSegment)
                        {
                            pointCollection.Add(lineSegment.Point);
                            currentPoint = lineSegment.Point;
                        }
                        else if (segment is PolyLineSegment polyLineSegment)
                        {
                            foreach (Point point in polyLineSegment.Points)
                            {
                                pointCollection.Add(point);
                                currentPoint = point;
                            }
                        }
                        // Поскольку геометрия сглажена, других типов сегментов быть не должно
                    }
                }

                // Вычисляем общую длину пути
                double totalLengthPixels = CalculatePathLength(pointCollection);
                double totalLengthMm = totalLengthPixels * pixelsToMm;

                // Дискретизируем с шагом stepMm
                if (pointCollection.Count >= 2)
                {
                    double accumulatedLength = 0;
                    for (int i = 1; i < pointCollection.Count; i++)
                    {
                        Point prevPoint = pointCollection[i - 1];
                        Point nextPoint = pointCollection[i];
                        double segmentLength = Distance(prevPoint, nextPoint) * pixelsToMm;

                        // Добавляем точки вдоль сегмента с шагом stepMm
                        for (double len = 0; len < segmentLength; len += stepMm)
                        {
                            double fraction = len / segmentLength;
                            Point interpolatedPoint = new Point(
                                prevPoint.X + (nextPoint.X - prevPoint.X) * fraction,
                                prevPoint.Y + (nextPoint.Y - prevPoint.Y) * fraction
                            );

                            // Преобразуем в 3D
                            double x = interpolatedPoint.X * pixelsToMm * scale + offset.X;
                            double y = interpolatedPoint.Y * pixelsToMm * scale + offset.Y;
                            y = -y; // Инвертируем Y

                            points.Add(new Point3D(x, y, offset.Z));
                        }

                        accumulatedLength += segmentLength;
                    }

                    // Добавляем последнюю точку
                    Point lastPoint = pointCollection[pointCollection.Count - 1];
                    double lastX = lastPoint.X * pixelsToMm * scale + offset.X;
                    double lastY = lastPoint.Y * pixelsToMm * scale + offset.Y;
                    points.Add(new Point3D(lastX, -lastY, offset.Z));
                }
            }

            return points;
        }

        // Вспомогательные методы
        private static double CalculatePathLength(List<Point> points)
        {
            double totalLength = 0;
            for (int i = 1; i < points.Count; i++)
            {
                totalLength += Distance(points[i - 1], points[i]);
            }
            return totalLength;
        }

        private static double Distance(Point a, Point b)
        {
            return Math.Sqrt(Math.Pow(b.X - a.X, 2) + Math.Pow(b.Y - a.Y, 2));
        }

        // УДАЛЕН дублирующий метод - оставляем только один
        private static List<Shapes_Path> GetPaths(DrawingGroup drawingGroup)
        {
            var paths = new List<Shapes_Path>();
            GetPathsFromDrawing(drawingGroup, paths, null);
            return paths;
        }

        private static void GetPathsFromDrawing(Drawing drawing, List<Shapes_Path> paths, Transform currentTransform)
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

                // Используем Transform геометрии, а не GeometryDrawing
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
                // Используем Shapes_Path вместо Path
                var path = new Shapes_Path
                {
                    Data = flattenedGeometry
                };
                paths.Add(path);
            }
        }
    }
}