using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Media.Media3D;

namespace FanucProgrammer
{
    /// <summary>
    /// Режим генерации текста
    /// </summary>
    public enum TextGenerationMode
    {
        /// <summary>
        /// Обводка контура (внутренний и внешний контур)
        /// </summary>
        Contour,

        /// <summary>
        /// Центральная линия (скелетизация, одна линия по центру)
        /// </summary>
        CenterLine
    }

    /// <summary>
    /// Генератор точек из текста (без использования SVG)
    /// </summary>
    public static class TextToPointsGenerator
    {
        // Коллекция для хранения загруженных пользовательских шрифтов
        private static PrivateFontCollection _customFonts = new PrivateFontCollection();

        /// <summary>
        /// Загружает пользовательский шрифт из файла
        /// </summary>
        public static bool LoadCustomFont(string fontFilePath)
        {
            try
            {
                if (!File.Exists(fontFilePath))
                {
                    Console.WriteLine($"Файл шрифта не найден: {fontFilePath}");
                    return false;
                }

                // Проверяем, не загружен ли уже этот шрифт
                string fileName = Path.GetFileName(fontFilePath);
                foreach (FontFamily family in _customFonts.Families)
                {
                    if (family.Name.Equals(fileName, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Шрифт уже загружен: {fileName}");
                        return true;
                    }
                }

                // Загружаем шрифт
                _customFonts.AddFontFile(fontFilePath);
                Console.WriteLine($"Шрифт загружен: {fontFilePath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при загрузке шрифта: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Получает список доступных шрифтов (системных + пользовательских)
        /// </summary>
        public static List<string> GetAvailableFonts()
        {
            var fonts = new List<string>();

            // Добавляем системные шрифты
            using (var systemFonts = new InstalledFontCollection())
            {
                foreach (FontFamily family in systemFonts.Families)
                {
                    fonts.Add(family.Name);
                }
            }

            // Добавляем пользовательские шрифты
            foreach (FontFamily family in _customFonts.Families)
            {
                if (!fonts.Contains(family.Name))
                {
                    fonts.Add(family.Name);
                }
            }

            return fonts.OrderBy(f => f).ToList();
        }

        /// <summary>
        /// Создает шрифт, поддерживая пользовательские шрифты
        /// </summary>
        private static Font CreateFont(string fontName, float fontSize)
        {
            try
            {
                // Сначала проверяем в пользовательских шрифтах
                foreach (FontFamily family in _customFonts.Families)
                {
                    if (family.Name.Equals(fontName, StringComparison.OrdinalIgnoreCase))
                    {
                        return new Font(family, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
                    }
                }

                // Если не найден в пользовательских, используем системный
                return new Font(fontName, fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании шрифта '{fontName}': {ex.Message}");
                // Возвращаем шрифт по умолчанию
                return new Font("Arial", fontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            }
        }

        /// <summary>
        /// Основной метод создания точек из текста с выбором режима
        /// </summary>
        public static List<Point3D> CreateTextPoints(
            string text,
            TextGenerationMode mode,
            string fontName = "Arial",
            float fontSize = 12,
            float depth = 50,
            float scale = 1.0f,
            Point3D offset = default,
            float liftHeight = 10.0f,
            float pointStep = 0.5f,
            bool separateCharacters = true)
                {
                    switch (mode)
                    {
                        case TextGenerationMode.Contour:
                            return separateCharacters ?
                                CreateTextPointsWithSegmentLift(text, fontName, fontSize, depth, scale,
                                    offset, liftHeight, pointStep) :
                                CreateTextPathPoints(text, fontName, fontSize, depth, scale,
                                    offset, liftHeight, pointStep);

                        case TextGenerationMode.CenterLine:
                            return CreateTextCenterLinePoints(text, fontName,
                                fontSize, depth, scale, offset, liftHeight, pointStep);

                        default:
                            return new List<Point3D>();
                    }
        }

        /// <summary>
        /// Создает точки из текста с использованием векторной графики (обводка контуром)
        /// </summary>
        public static List<Point3D> CreateTextPathPoints(
            string text,
            string fontName = "Arial",
            float fontSize = 12,
            float depth = 50,
            float scale = 1.0f,
            Point3D offset = default,
            float liftHeight = 10.0f,
            float pointStep = 0.5f)
        {
            var allPoints = new List<Point3D>();

            if (string.IsNullOrWhiteSpace(text))
                return allPoints;

            try
            {
                // Создаем временный Bitmap для измерения
                using (Bitmap bmp = new Bitmap(1000, 1000))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    // Создаем шрифт с поддержкой пользовательских шрифтов
                    Font font = CreateFont(fontName, fontSize);

                    // Разбиваем текст на строки
                    string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    float currentY = 0;

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            currentY += fontSize * 1.5f; // Межстрочный интервал
                            continue;
                        }

                        // Получаем ширину строки
                        SizeF lineSize = g.MeasureString(line, font);

                        // Центрируем строку по X
                        float lineCenterX = lineSize.Width / 2;

                        // Для каждого символа в строке
                        for (int i = 0; i < line.Length; i++)
                        {
                            char currentChar = line[i];

                            if (currentChar == ' ')
                            {
                                // Для пробела просто пропускаем
                                continue;
                            }

                            // Измеряем символ
                            SizeF charSize = g.MeasureString(currentChar.ToString(), font);

                            // Создаем GraphicsPath для одного символа
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                // Добавляем символ в путь
                                path.AddString(
                                    currentChar.ToString(),
                                    font.FontFamily,
                                    (int)font.Style,
                                    font.Size,
                                    new PointF(0, currentY),
                                    StringFormat.GenericTypographic);

                                // Получаем ограничивающий прямоугольник
                                RectangleF bounds = path.GetBounds();

                                // Центрируем символ
                                float centerX = bounds.Width / 2;
                                float centerY = bounds.Height / 2;

                                // Упрощаем путь с учетом шага точек
                                path.Flatten(new Matrix(), pointStep);

                                var pathPoints = path.PathPoints;

                                if (pathPoints.Length > 0)
                                {
                                    // Если это не первый символ в строке, добавляем точку подъема
                                    if (i > 0 && line[i - 1] != ' ')
                                    {
                                        var lastPoint = allPoints.LastOrDefault();
                                        if (allPoints.Count > 0)
                                        {
                                            // Поднимаемся
                                            allPoints.Add(new Point3D(
                                                lastPoint.X,
                                                lastPoint.Y,
                                                depth + liftHeight));

                                            // Перемещаемся к началу нового символа (опускаемся позже)
                                            float x = (pathPoints[0].X - centerX) * scale + (float)offset.X;
                                            float y = (pathPoints[0].Y - centerY) * scale + (float)offset.Y + currentY;
                                            allPoints.Add(new Point3D(x, y, depth + liftHeight));
                                        }
                                    }

                                    // Добавляем точки символа
                                    foreach (var point in pathPoints)
                                    {
                                        // Применяем масштаб и смещение
                                        float x = (point.X - centerX) * scale + (float)offset.X;
                                        float y = (point.Y - centerY) * scale + (float)offset.Y + currentY;

                                        allPoints.Add(new Point3D(x, y, depth));
                                    }

                                    // Замыкаем контур символа (если нужно)
                                    if (pathPoints.Length > 0)
                                    {
                                        var firstPoint = pathPoints[0];
                                        float x = (firstPoint.X - centerX) * scale + (float)offset.X;
                                        float y = (firstPoint.Y - centerY) * scale + (float)offset.Y + currentY;
                                        allPoints.Add(new Point3D(x, y, depth));
                                    }

                                    // Поднимаемся после завершения символа
                                    if (pathPoints.Length > 0)
                                    {
                                        var lastPoint = pathPoints[pathPoints.Length - 1];
                                        float x = (lastPoint.X - centerX) * scale + (float)offset.X;
                                        float y = (lastPoint.Y - centerY) * scale + (float)offset.Y + currentY;
                                        allPoints.Add(new Point3D(x, y, depth + liftHeight));
                                    }
                                }
                            }
                        }

                        // После строки поднимаемся
                        if (allPoints.Count > 0)
                        {
                            var lastPoint = allPoints.Last();
                            allPoints.Add(new Point3D(
                                lastPoint.X,
                                lastPoint.Y,
                                depth + liftHeight));
                        }

                        currentY += fontSize * 1.5f; // Межстрочный интервал
                    }

                    font.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании текстового пути: {ex.Message}");
            }

            return allPoints;
        }

        /// <summary>
        /// Создает точки с подъемом между каждым сегментом (обводка контуром)
        /// </summary>
        public static List<Point3D> CreateTextPointsWithSegmentLift(
            string text,
            string fontName = "Arial",
            float fontSize = 12,
            float depth = 50,
            float scale = 1.0f,
            Point3D offset = default,
            float liftHeight = 10.0f,
            float pointStep = 0.5f)
        {
            var points = new List<Point3D>();

            if (string.IsNullOrWhiteSpace(text))
                return points;

            try
            {
                // Создаем временный Bitmap
                using (Bitmap bmp = new Bitmap(1000, 1000))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    // Создаем шрифт с поддержкой пользовательских шрифтов
                    Font font = CreateFont(fontName, fontSize);

                    // Создаем GraphicsPath для всего текста
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddString(
                            text,
                            font.FontFamily,
                            (int)font.Style,
                            font.Size,
                            new PointF(0, 0),
                            StringFormat.GenericTypographic);

                        // Получаем ограничивающий прямоугольник
                        RectangleF bounds = path.GetBounds();
                        float centerX = bounds.Width / 2;
                        float centerY = bounds.Height / 2;

                        // Разбиваем путь на подпути (символы)
                        var subPaths = SplitPathByCharacters(path, text, g, font);

                        bool firstCharacter = true;

                        foreach (var subPath in subPaths)
                        {
                            if (!firstCharacter)
                            {
                                // Поднимаемся перед началом нового символа
                                var lastPoint = points.LastOrDefault();
                                if (points.Count > 0)
                                {
                                    points.Add(new Point3D(
                                        lastPoint.X,
                                        lastPoint.Y,
                                        depth + liftHeight));
                                }
                            }

                            // Преобразуем подпуть в точки с учетом шага
                            subPath.Flatten(new Matrix(), pointStep);
                            var subPoints = subPath.PathPoints;

                            foreach (var point in subPoints)
                            {
                                float x = (point.X - centerX) * scale + (float)offset.X;
                                float y = (point.Y - centerY) * scale + (float)offset.Y;
                                points.Add(new Point3D(x, y, depth));
                            }

                            // Замыкаем символ
                            if (subPoints.Length > 0)
                            {
                                float x = (subPoints[0].X - centerX) * scale + (float)offset.X;
                                float y = (subPoints[0].Y - centerY) * scale + (float)offset.Y;
                                points.Add(new Point3D(x, y, depth));
                            }

                            firstCharacter = false;

                            // Поднимаемся после символа
                            if (subPoints.Length > 0)
                            {
                                float x = (subPoints[subPoints.Length - 1].X - centerX) * scale + (float)offset.X;
                                float y = (subPoints[subPoints.Length - 1].Y - centerY) * scale + (float)offset.Y;
                                points.Add(new Point3D(x, y, depth + liftHeight));
                            }
                        }
                    }

                    font.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
            }

            return points;
        }

        /// <summary>
        /// Создает точки центральной линии букв (скелетизация)
        /// </summary>
        public static List<Point3D> CreateTextCenterLinePoints(
    string text,
    string fontName = "Arial",
    float fontSize = 12,
    float depth = 50,
    float scale = 1.0f,
    Point3D offset = default,
    float liftHeight = 10.0f,
    float pointStep = 0.5f)
        {
            var points = new List<Point3D>();

            if (string.IsNullOrWhiteSpace(text))
                return points;

            try
            {
                // Создаем временный Bitmap
                using (Bitmap bmp = new Bitmap(1000, 1000))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                    // Создаем шрифт с поддержкой пользовательских шрифтов
                    Font font = CreateFont(fontName, fontSize);

                    // Получаем размеры текста
                    SizeF textSize = g.MeasureString(text, font);

                    // Разбиваем текст на строки
                    float currentY = 0;
                    string[] lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    foreach (string line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            currentY += fontSize * 1.5f;
                            continue;
                        }

                        // Измеряем ширину строки для центрирования
                        SizeF lineSize = g.MeasureString(line, font);
                        float lineOffsetX = lineSize.Width / 2;

                        // Обрабатываем каждый символ отдельно
                        float charX = 0;

                        for (int i = 0; i < line.Length; i++)
                        {
                            char currentChar = line[i];

                            if (currentChar == ' ')
                            {
                                // Для пробела просто добавляем смещение
                                SizeF spaceSize = g.MeasureString(" ", font);
                                charX += spaceSize.Width;
                                continue;
                            }

                            // Измеряем символ
                            string charStr = currentChar.ToString();
                            SizeF charSize = g.MeasureString(charStr, font);

                            // Создаем GraphicsPath для символа
                            using (GraphicsPath path = new GraphicsPath())
                            {
                                path.AddString(
                                    charStr,
                                    font.FontFamily,
                                    (int)font.Style,
                                    font.Size,
                                    new PointF(charX, currentY),
                                    StringFormat.GenericTypographic);

                                // Получаем ограничивающий прямоугольник
                                RectangleF bounds = path.GetBounds();

                                // Вычисляем точки центральной линии
                                var centerLinePoints = CalculateCenterLine(path, bounds, pointStep);

                                if (centerLinePoints.Count > 0)
                                {
                                    // Если это не первый символ в строке и не первый символ вообще, добавляем подъем
                                    if (i > 0 && line[i - 1] != ' ' && points.Count > 0)
                                    {
                                        var lastPoint = points.Last();

                                        // Поднимаемся
                                        points.Add(new Point3D(
                                            lastPoint.X,
                                            lastPoint.Y,
                                            depth + liftHeight));

                                        // Перемещаемся к началу нового символа
                                        float firstX = (centerLinePoints[0].X - bounds.Width / 2 - lineOffsetX) * scale + (float)offset.X;
                                        float firstY = (centerLinePoints[0].Y - bounds.Height / 2) * scale + (float)offset.Y + currentY;
                                        points.Add(new Point3D(firstX, firstY, depth + liftHeight));
                                    }

                                    // Добавляем точки центральной линии
                                    foreach (var point in centerLinePoints)
                                    {
                                        // Центрируем символ относительно начала строки
                                        float x = (point.X - bounds.Width / 2 - lineOffsetX) * scale + (float)offset.X;
                                        float y = (point.Y - bounds.Height / 2) * scale + (float)offset.Y + currentY;

                                        points.Add(new Point3D(x, y, depth));
                                    }

                                    // Поднимаемся после символа
                                    if (centerLinePoints.Count > 0)
                                    {
                                        var lastCenterPoint = centerLinePoints.Last();
                                        float lastX = (lastCenterPoint.X - bounds.Width / 2 - lineOffsetX) * scale + (float)offset.X;
                                        float lastY = (lastCenterPoint.Y - bounds.Height / 2) * scale + (float)offset.Y + currentY;
                                        points.Add(new Point3D(lastX, lastY, depth + liftHeight));
                                    }
                                }

                                // Смещаем позицию для следующего символа
                                charX += charSize.Width;
                            }
                        }

                        // После строки добавляем дополнительный подъем
                        if (points.Count > 0)
                        {
                            var lastPoint = points.Last();
                            points.Add(new Point3D(
                                lastPoint.X,
                                lastPoint.Y,
                                depth + liftHeight * 1.5f));
                        }

                        currentY += fontSize * 1.5f;
                    }

                    font.Dispose();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при создании центральной линии: {ex.Message}");

                // В случае ошибки используем контурный метод как запасной вариант
                points = CreateTextPathPoints(text, fontName, fontSize, depth, scale, offset, liftHeight, pointStep);
            }

            return points;
        }

        /// <summary>
        /// Вычисляет точки центральной линии для пути
        /// </summary>
        private static List<PointF> CalculateCenterLine(GraphicsPath path, RectangleF bounds, float pointStep)
        {
            var centerLinePoints = new List<PointF>();

            try
            {
                // Упрощаем путь для получения точек
                path.Flatten(new Matrix(), pointStep);
                var pathPoints = path.PathPoints;

                if (pathPoints.Length < 2)
                    return centerLinePoints;

                // Разбиваем путь на отдельные контуры (на основе пустых интервалов)
                var contours = new List<List<PointF>>();
                var currentContour = new List<PointF>();

                // Используем допуск для определения разрыва между контурами
                float distanceThreshold = pointStep * 3;

                for (int i = 0; i < pathPoints.Length; i++)
                {
                    if (currentContour.Count > 0)
                    {
                        var lastPoint = currentContour.Last();
                        var distance = Math.Sqrt(
                            Math.Pow(pathPoints[i].X - lastPoint.X, 2) +
                            Math.Pow(pathPoints[i].Y - lastPoint.Y, 2));

                        if (distance > distanceThreshold)
                        {
                            // Начался новый контур
                            if (currentContour.Count > 2)
                            {
                                contours.Add(new List<PointF>(currentContour));
                            }
                            currentContour.Clear();
                        }
                    }

                    currentContour.Add(pathPoints[i]);
                }

                if (currentContour.Count > 2)
                {
                    contours.Add(currentContour);
                }

                // Если не удалось выделить контуры, используем все точки
                if (contours.Count == 0 && pathPoints.Length > 2)
                {
                    contours.Add(new List<PointF>(pathPoints));
                }

                // Для каждого контура находим центральную линию
                foreach (var contour in contours)
                {
                    if (contour.Count < 3) continue;

                    // Находим среднюю линию по Y для каждого X
                    var pointsByX = new Dictionary<float, List<float>>();

                    foreach (var point in contour)
                    {
                        float roundedX = (float)Math.Round(point.X / pointStep) * pointStep;
                        if (!pointsByX.ContainsKey(roundedX))
                        {
                            pointsByX[roundedX] = new List<float>();
                        }
                        pointsByX[roundedX].Add(point.Y);
                    }

                    // Для каждого X находим min и max Y, затем среднюю точку
                    var sortedX = pointsByX.Keys.OrderBy(x => x).ToList();
                    foreach (var x in sortedX)
                    {
                        var yValues = pointsByX[x];
                        if (yValues.Count >= 2)
                        {
                            float minY = yValues.Min();
                            float maxY = yValues.Max();
                            float centerY = (minY + maxY) / 2;

                            // Добавляем точку только если она существенно отличается от предыдущей
                            if (centerLinePoints.Count == 0 ||
                                Math.Abs(centerY - centerLinePoints.Last().Y) > pointStep / 2)
                            {
                                centerLinePoints.Add(new PointF(x, centerY));
                            }
                        }
                        else if (yValues.Count == 1)
                        {
                            // Для одиночных точек (например, в середине буквы "i")
                            centerLinePoints.Add(new PointF(x, yValues[0]));
                        }
                    }
                }

                // Сортируем точки по X для правильного порядка
                centerLinePoints = centerLinePoints
                    .OrderBy(p => p.X)
                    .ThenBy(p => p.Y)
                    .ToList();

                // Упрощаем линию (удаляем близко расположенные точки)
                if (centerLinePoints.Count > 2)
                {
                    centerLinePoints = SimplifyCenterLine(centerLinePoints, pointStep);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при вычислении центральной линии: {ex.Message}");

                // В случае ошибки возвращаем среднюю точку контура как запасной вариант
                if (centerLinePoints.Count == 0)
                {
                    float centerX = bounds.X + bounds.Width / 2;
                    float centerY = bounds.Y + bounds.Height / 2;
                    centerLinePoints.Add(new PointF(centerX, centerY));
                }
            }

            return centerLinePoints;
        }

        /// <summary>
        /// Упрощает центральную линию, удаляя лишние точки
        /// </summary>
        private static List<PointF> SimplifyCenterLine(List<PointF> points, float tolerance)
        {
            if (points.Count < 3)
                return points;

            var simplified = new List<PointF> { points[0] };

            for (int i = 1; i < points.Count - 1; i++)
            {
                // Проверяем, лежит ли точка примерно на прямой между предыдущей и следующей точками
                var prev = simplified.Last();
                var next = points[i + 1];
                var current = points[i];

                // Вычисляем расстояние от текущей точки до линии между prev и next
                float distance = PointToLineDistance(current, prev, next);

                if (distance > tolerance)
                {
                    simplified.Add(current);
                }
            }

            simplified.Add(points[points.Count - 1]);
            return simplified;
        }


        private static float PointToLineDistance(PointF point, PointF lineStart, PointF lineEnd)
        {
            float A = point.X - lineStart.X;
            float B = point.Y - lineStart.Y;
            float C = lineEnd.X - lineStart.X;
            float D = lineEnd.Y - lineStart.Y;

            float dot = A * C + B * D;
            float lenSq = C * C + D * D;
            float param = (lenSq != 0) ? dot / lenSq : -1;

            float xx, yy;

            if (param < 0)
            {
                xx = lineStart.X;
                yy = lineStart.Y;
            }
            else if (param > 1)
            {
                xx = lineEnd.X;
                yy = lineEnd.Y;
            }
            else
            {
                xx = lineStart.X + param * C;
                yy = lineStart.Y + param * D;
            }

            float dx = point.X - xx;
            float dy = point.Y - yy;

            return (float)Math.Sqrt(dx * dx + dy * dy);
        }


       

        /// <summary>
        /// Разделяет путь на подпути по символам
        /// </summary>
        private static List<GraphicsPath> SplitPathByCharacters(
            GraphicsPath mainPath,
            string text,
            Graphics g,
            Font font)
        {
            var subPaths = new List<GraphicsPath>();

            try
            {
                // Измеряем позиции символов
                float currentX = 0;

                for (int i = 0; i < text.Length; i++)
                {
                    char c = text[i];
                    if (c == ' ')
                    {
                        // Пропускаем пробелы
                        SizeF spaceSize = g.MeasureString(" ", font);
                        currentX += spaceSize.Width;
                        continue;
                    }

                    // Измеряем символ
                    SizeF charSize = g.MeasureString(c.ToString(), font);

                    // Создаем подпуть для символа
                    var subPath = new GraphicsPath();
                    subPath.AddString(
                        c.ToString(),
                        font.FontFamily,
                        (int)font.Style,
                        font.Size,
                        new PointF(currentX, 0),
                        StringFormat.GenericTypographic);

                    subPaths.Add(subPath);

                    currentX += charSize.Width;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при разделении пути: {ex.Message}");
            }

            return subPaths;
        }
    }
}