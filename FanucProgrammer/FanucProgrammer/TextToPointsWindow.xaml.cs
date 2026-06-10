using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Media3D;

namespace FanucProgrammer
{
    public partial class TextToPointsWindow : Window
    {
        private string currentText = string.Empty;
        private List<string> availableFonts = new List<string>();

        public TextToPointsWindow()
        {
            InitializeComponent();
            LoadAvailableFonts();
            InitializeTextModeComboBox();
        }

        private void LoadAvailableFonts()
        {
            try
            {
                // Получаем список доступных шрифтов
                availableFonts = TextToPointsGenerator.GetAvailableFonts();

                // Заполняем ComboBox
                FontComboBox.Items.Clear();
                foreach (var font in availableFonts)
                {
                    FontComboBox.Items.Add(font);
                }

                // Выбираем Arial по умолчанию
                if (availableFonts.Contains("Arial"))
                    FontComboBox.SelectedItem = "Arial";
                else if (availableFonts.Count > 0)
                    FontComboBox.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке списка шрифтов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void InitializeTextModeComboBox()
        {
            // Заполняем ComboBox режимами генерации текста
            TextModeComboBox.Items.Clear();
            TextModeComboBox.Items.Add(new ComboBoxItem { Content = "Обводка контуром", Tag = TextGenerationMode.Contour });
            TextModeComboBox.Items.Add(new ComboBoxItem { Content = "Центральная линия", Tag = TextGenerationMode.CenterLine });
            TextModeComboBox.SelectedIndex = 0; // По умолчанию - обводка контуром
        }

        private void LoadCustomFontButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Файлы шрифтов (*.ttf;*.otf;*.fon)|*.ttf;*.otf;*.fon|Все файлы (*.*)|*.*",
                Title = "Выберите файл шрифта"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    // Загружаем шрифт
                    if (TextToPointsGenerator.LoadCustomFont(dialog.FileName))
                    {
                        // Обновляем список шрифтов
                        LoadAvailableFonts();

                        // Выбираем загруженный шрифт
                        string fontName = Path.GetFileNameWithoutExtension(dialog.FileName);
                        if (FontComboBox.Items.Contains(fontName))
                            FontComboBox.SelectedItem = fontName;

                        MessageBox.Show($"Шрифт успешно загружен: {fontName}", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show("Не удалось загрузить шрифт", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке шрифта: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadWordButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Word документы (*.docx)|*.docx|Все файлы (*.*)|*.*",
                Title = "Выберите Word документ"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string text = ExtractTextFromWord(dialog.FileName);
                    TextInputTextBox.Text = text;
                    currentText = text;
                    UpdateStatus($"Загружен Word документ: {System.IO.Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке Word документа: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private string ExtractTextFromWord(string filePath)
        {
            StringBuilder textBuilder = new StringBuilder();

            using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, false))
            {
                Body body = doc.MainDocumentPart.Document.Body;

                if (body != null)
                {
                    foreach (var paragraph in body.Elements<Paragraph>())
                    {
                        string paragraphText = string.Join("", paragraph.Elements<Run>()
                            .Select(r => string.Join("", r.Elements<Text>().Select(t => t.Text))));

                        if (!string.IsNullOrWhiteSpace(paragraphText))
                        {
                            textBuilder.AppendLine(paragraphText);
                        }
                    }
                }
            }

            return textBuilder.ToString();
        }

        private void LoadTextButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите текстовый файл"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    string text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    TextInputTextBox.Text = text;
                    currentText = text;
                    UpdateStatus($"Загружен текстовый файл: {System.IO.Path.GetFileName(dialog.FileName)}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void GeneratePointsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string text = TextInputTextBox.Text;
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("Введите текст для преобразования", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Получаем параметры из формы
                string fontFamily = FontComboBox.SelectedItem?.ToString() ?? "Arial";
                if (!float.TryParse(FontSizeTextBox.Text, out float fontSize))
                    fontSize = 12;
                if (!float.TryParse(LineSpacingTextBox.Text, out float lineSpacing))
                    lineSpacing = 15;
                if (!float.TryParse(DepthTextBox.Text, out float depth))
                    depth = 50;
                if (!float.TryParse(ScaleTextBox.Text, out float scale))
                    scale = 1.0f;
                if (!float.TryParse(LiftHeightTextBox.Text, out float liftHeight))
                    liftHeight = 10.0f;
                if (!float.TryParse(PointStepTextBox.Text, out float pointStep))
                    pointStep = 0.5f;

                // Получаем выбранный режим генерации
                TextGenerationMode textMode = TextGenerationMode.Contour;
                if (TextModeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag is TextGenerationMode mode)
                {
                    textMode = mode;
                }

                bool separateCharacters = SeparateCharactersCheckBox.IsChecked == true;

                // Генерируем точки с учетом выбранного режима
                List<Point3D> points = TextToPointsGenerator.CreateTextPoints(
                    text: text,
                    mode: textMode,
                    fontName: fontFamily,
                    fontSize: fontSize,
                    depth: depth,
                    scale: scale,
                    offset: new Point3D(0, 0, 0),
                    liftHeight: liftHeight,
                    pointStep: pointStep,
                    separateCharacters: separateCharacters);

                // Отображаем информацию
                StringBuilder info = new StringBuilder();
                info.AppendLine($"Сгенерировано точек: {points.Count}");
                info.AppendLine($"Шрифт: {fontFamily}");
                info.AppendLine($"Размер шрифта: {fontSize} мм");
                info.AppendLine($"Глубина (Z): {depth} мм");
                info.AppendLine($"Высота подъема: {liftHeight} мм");
                info.AppendLine($"Масштаб: {scale}");
                info.AppendLine($"Шаг точек: {pointStep} мм");
                info.AppendLine($"Режим: {textMode}");
                info.AppendLine($"Раздельные символы: {separateCharacters}");

                PointsInfoTextBox.Text = info.ToString();

                // Передаем точки в главное окно
                CopyPointsToMainWindow(points, fontFamily, fontSize, lineSpacing, depth,
                    scale, liftHeight, text, pointStep, textMode, separateCharacters);

                UpdateStatus($"Точки сгенерированы: {points.Count} точек из текста");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при генерации точек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyPointsToMainWindow(List<Point3D> points, string fontFamily, float fontSize,
                           float lineSpacing, float depth, float scale, float liftHeight,
                           string originalText, float pointStep,
                           TextGenerationMode textMode, bool separateCharacters)
        {
            // Ищем главное окно PointsOnModelWindow
            var mainWindow = Application.Current.Windows.OfType<PointsOnModelWindow>().FirstOrDefault();
            if (mainWindow != null)
            {
                // Теперь метод доступен, так как он public
                mainWindow.LoadTextPoints(points, fontFamily, fontSize, lineSpacing,
                    depth, scale, liftHeight, originalText, pointStep, textMode, separateCharacters);

                MessageBox.Show($"Точки переданы в окно модели: {points.Count} точек\n" +
                               $"Режим: {textMode}\n" +
                               $"Раздельные символы: {separateCharacters}\n" +
                               $"Шаг точек: {pointStep} мм",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                this.Close();
            }
            else
            {
                MessageBox.Show($"Сгенерировано {points.Count} точек. Откройте окно модели для отображения.",
                    "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateStatus(string message)
        {
            Console.WriteLine($"TextToPointsWindow: {message}");
        }
    }
}