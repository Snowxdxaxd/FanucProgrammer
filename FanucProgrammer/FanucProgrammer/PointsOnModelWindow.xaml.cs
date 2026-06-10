using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using static FanucProgrammer.SvgCalligraphyProcessor;
using IO_Path = System.IO.Path;
using Shapes_Path = System.Windows.Shapes.Path;

namespace FanucProgrammer
{
    public partial class PointsOnModelWindow : Window
    {
        private string lastSvgFilePath = string.Empty;
        private Model3DGroup sceneGroup;
        private Model3DGroup modelPointsGroup;
        private Model3DGroup svgPointsGroup;
        private readonly List<Point3D> modelPoints = new List<Point3D>();
        private readonly List<Point3D> svgPoints = new List<Point3D>();
        private readonly List<Model3D> loadedModels = new List<Model3D>();
        private Model3D lastLoadedModel;
        private TranslateTransform3D lastLoadedModelTranslation = new TranslateTransform3D();
        private const float WORK_ZONE_MIN_X = -100f;
        private const float WORK_ZONE_MAX_X = 100f;
        private const float WORK_ZONE_MIN_Y = -100f;
        private const float WORK_ZONE_MAX_Y = 100f;
        private const float WORK_ZONE_MIN_Z = 0f;
        private const float WORK_ZONE_MAX_Z = 100f;

        private double angleX = 0;
        private double angleY = 0;
        private double cameraDistance = 1000;
        private Point3D cameraTarget = new Point3D(0, 0, 0);

        // Используем старые правильные константы из второго кода
        public const double A4_SHEET_WIDTH = 210.0;   // 210 мм (X от -40 до 170)
        public const double A4_SHEET_HEIGHT = 290.0;  // 290 мм (Y от -200 до 90)

        // Границы листа как в изображении
        public const double A4_SHEET_MIN_X = -25.0;   // -105 мм от центра
        public const double A4_SHEET_MAX_X = 185.0;    // 105 мм от центра
        public const double A4_SHEET_MIN_Y = -140.0;   // -145 мм от центра
        public const double A4_SHEET_MAX_Y = 150.0;
        public const double A4_SHEET_MIN_Z = 0;

        private bool _invertGrid = true;

        // Размеры ячеек сетки (7x10 ячеек)
        private const double CELL_WIDTH = 30.0;    // 210 мм / 7 = 30 мм на ячейку
        private const double CELL_HEIGHT = 29.0;    // Верхний край Y (297 мм от -200) - ИЗМЕНИТЬ с 90 на 97

        // Константы для сетки (7×10 ячеек)
        private const int GRID_CELLS_X = 7;   // 7 ячеек по ширине
        private const int GRID_CELLS_Y = 10;  // 10 ячеек по высоте

        private Point lastMousePosition;
        private bool isLeftMouseButtonPressed = false;
        private bool isRightMouseButtonPressed = false;
        private const double MOUSE_ROTATION_SPEED = 0.3;
        private const double MOUSE_PAN_SPEED = 0.5;
        private const double MOUSE_ZOOM_SPEED = 0.001;

        private double svgRotationAngle = 0;
        private const bool APPLY_COORDINATE_CORRECTION = true;
        private const double COORDINATE_CORRECTION_ANGLE = -90.0;

        private readonly List<Point3D> textPoints = new List<Point3D>();
        private Model3DGroup textPointsGroup;

        private double textOffsetX = 0;
        private double textOffsetY = 0;
        private double textOffsetZ = 50;
        private double textScale = 1.0;
        private double textRotation = 0;
        private List<Point3D> originalTextPoints = new List<Point3D>();
        private string textFontFamily = "Arial";
        private float textFontSize = 12;
        private float textLineSpacing = 15;
        private float textDepth = 50;
        private float textLiftHeight = 10.0f;

        private string originalText = string.Empty;
        private float textPointStep = 0.5f;
        private bool textInvertX = false;
        private bool textInvertY = false;
        private bool textSeparateCharacters = true;
        private TextGenerationMode textGenerationMode = TextGenerationMode.Contour;

        private List<SvgCalligraphyProcessor.CalligraphyStroke> calligraphyStrokes = new List<SvgCalligraphyProcessor.CalligraphyStroke>();
        private double strokeWidthVariationForR = 0.0;

        private SceneObjectManager _sceneManager;
        private DateTime _lastClickTime = DateTime.MinValue;
        private const double DOUBLE_CLICK_MS = 300;

        private TextBox _objectNameTextBox;
        private TextBox _objectOffsetXTextBox;
        private TextBox _objectOffsetYTextBox;
        private TextBox _objectOffsetZTextBox;
        private TextBox _objectScaleTextBox;
        private TextBox _objectRotationTextBox;
        private Border _objectEditPanel;
        private SceneObject _currentEditObject;
        private TextBlock _cellInfoLabel;

        private bool _isSheetGridVisible = false;
        private Model3DGroup _sheetGridGroup;
        private const double GRID_STEP = 10.0;
        private List<SceneObject> _gridAttachedObjects = new List<SceneObject>();
        private bool _isGridSnapEnabled = false;
        private Button _toggleGridButton;
        private CheckBox _gridSnapCheckBox;

        private List<GridCell> _gridCells = new List<GridCell>();
        private Model3DGroup _gridNumbersGroup;
        private Dictionary<string, int> _objectToCellMap = new Dictionary<string, int>();
        private Dictionary<int, string> _cellToObjectMap = new Dictionary<int, string>();
        private const double GRID_CELL_SIZE = 20.0;
        private bool _isNumberedGridVisible = false;
        private CheckBox _numberedGridCheckBox;
        private TextBox _assignCellTextBox;

        public enum SceneObjectType { Model3D, SVG, Text, Calligraphy }

        private TextBox _objectCellNumberTextBox;
        private TextBox _objectStrokeWidthTextBox;
        private CheckBox _objectCalligraphyEnabledCheckBox;
        private ComboBox _objectCalligraphyModeComboBox;

        private SheetTransform _sheetTransform = new SheetTransform();
        private SheetTransform _gridTransform = new SheetTransform();
        
        private bool _isGridAttachedToSheet = true;
        private Transform3DGroup _currentSheetTransform;
        private Transform3DGroup _currentGridTransform;
        private Model3DGroup _sheetGroup;

        private bool _isSheetTransformApplied = false;
        private bool _isGridTransformApplied = false;


        public class SheetTransform
        {
            public double OffsetX { get; set; } = 0;
            public double OffsetY { get; set; } = 0;
            public double OffsetZ { get; set; } = 0;
            public double Rotation { get; set; } = 0;
            public double Scale { get; set; } = 1.0;

            public Transform3DGroup GetTransform()
            {
                var transformGroup = new Transform3DGroup();

                // 1. Масштаб (применяется первым к локальным координатам)
                if (Math.Abs(Scale - 1.0) > 0.001)
                {
                    transformGroup.Children.Add(new ScaleTransform3D(Scale, Scale, Scale));
                }

                // 2. Поворот вокруг центра листа
                if (Math.Abs(Rotation) > 0.001)
                {
                    // Для правильного поворота вокруг центра нужно:
                    // 1. Сместить в центр
                    // 2. Повернуть
                    // 3. Вернуть обратно
                    double centerX = (A4_SHEET_MIN_X + A4_SHEET_MAX_X) / 2;
                    double centerY = (A4_SHEET_MIN_Y + A4_SHEET_MAX_Y) / 2;

                    var rotateTransform = new RotateTransform3D(
                        new AxisAngleRotation3D(new Vector3D(0, 0, 1), Rotation));

                    // Создаем трансформацию для поворота вокруг центра
                    var centerTransform = new Transform3DGroup();
                    centerTransform.Children.Add(new TranslateTransform3D(-centerX, -centerY, 0));
                    centerTransform.Children.Add(rotateTransform);
                    centerTransform.Children.Add(new TranslateTransform3D(centerX, centerY, 0));

                    transformGroup.Children.Add(centerTransform);
                }

                // 3. Смещение
                if (Math.Abs(OffsetX) > 0.001 || Math.Abs(OffsetY) > 0.001 || Math.Abs(OffsetZ) > 0.001)
                {
                    transformGroup.Children.Add(new TranslateTransform3D(OffsetX, OffsetY, OffsetZ));
                }

                return transformGroup;
            }


            public Point3D TransformPoint(Point3D point)
            {
                var transform = GetTransform();
                return transform.Transform(point);
            }

            public Point3D InverseTransformPoint(Point3D point)
            {
                var transform = GetTransform();
                var inverseTransform = transform.Inverse;
                if (inverseTransform != null)
                {
                    return inverseTransform.Transform(point);
                }
                return point;
            }
        }

        public class SceneObject
        {
            public Guid Id { get; } = Guid.NewGuid();
            public SceneObjectType Type { get; set; }
            public string Name { get; set; }
            public bool IsVisible { get; set; } = true;
            public bool IsGridAttached { get; set; } = false;

            public double OffsetX { get; set; }
            public double OffsetY { get; set; }
            public double OffsetZ { get; set; }
            public double Scale { get; set; } = 1.0;
            public double Rotation { get; set; }

            public string FilePath { get; set; }
            public string TextContent { get; set; }
            public string FontFamily { get; set; }
            public float FontSize { get; set; }
            public CalligraphyMode CalligraphyMode { get; set; }
            public double StrokeWidth { get; set; }

            public Model3DGroup VisualModel { get; set; }
            public Transform3DGroup TransformGroup { get; set; }

            public List<Point3D> Points { get; set; } = new List<Point3D>();
            public List<SvgCalligraphyProcessor.CalligraphyStroke> CalligraphyStrokes { get; set; } = new List<SvgCalligraphyProcessor.CalligraphyStroke>();
        }

        private class GridCell
        {
            public int CellNumber { get; set; }
            public Rect Bounds { get; set; } // Локальные координаты
            public Point3D Center { get; set; } // Мировые координаты (с коррекцией)
            public Point3D LocalCenter { get; set; } // Локальные координаты (без коррекции)
            public bool HasObject { get; set; }
            public string ObjectName { get; set; }
        }

        public class SceneObjectManager
        {
            private readonly PointsOnModelWindow _window;
            private readonly List<SceneObject> _objects = new List<SceneObject>();
            private SceneObject _selectedObject;

            public SceneObjectManager(PointsOnModelWindow window)
            {
                _window = window;
            }

            public void AddObject(SceneObject obj)
            {
                _objects.Add(obj);
            }

            public bool TrySelectObject(Point3D hitPoint)
            {
                SceneObject closest = null;
                double minDistance = double.MaxValue;

                foreach (var obj in _objects)
                {
                    if (!obj.IsVisible || obj.VisualModel == null)
                        continue;

                    var bounds = obj.VisualModel.Bounds;
                    if (bounds.Contains(hitPoint))
                    {
                        var center = new Point3D(
                            bounds.X + bounds.SizeX / 2,
                            bounds.Y + bounds.SizeY / 2,
                            bounds.Z + bounds.SizeZ / 2
                        );

                        double distance = Distance(center, hitPoint);
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            closest = obj;
                        }
                    }
                }

                if (closest != null)
                {
                    _selectedObject = closest;
                    return true;
                }

                return false;
            }

            private double Distance(Point3D p1, Point3D p2)
            {
                return Math.Sqrt(
                    Math.Pow(p2.X - p1.X, 2) +
                    Math.Pow(p2.Y - p1.Y, 2) +
                    Math.Pow(p2.Z - p1.Z, 2)
                );
            }

            public SceneObject SelectedObject => _selectedObject;

            public void Deselect()
            {
                _selectedObject = null;
            }

            public IEnumerable<SceneObject> GetAllObjects() => _objects;

            public void RemoveObject(Guid id)
            {
                var obj = _objects.FirstOrDefault(o => o.Id == id);
                if (obj != null)
                {
                    _objects.Remove(obj);
                    if (obj.VisualModel != null)
                    {
                        _window.sceneGroup.Children.Remove(obj.VisualModel);
                    }
                }
            }
        }

        public PointsOnModelWindow()
        {
            InitializeComponent();


            sceneGroup = (Model3DGroup)((ModelVisual3D)Viewport3D.Children[0]).Content;
            modelPointsGroup = new Model3DGroup();
            sceneGroup.Children.Add(modelPointsGroup);

            svgPointsGroup = new Model3DGroup();
            sceneGroup.Children.Add(svgPointsGroup);

            _sheetGroup = new Model3DGroup();
            sceneGroup.Children.Add(_sheetGroup);

            _sheetGridGroup = new Model3DGroup();
            sceneGroup.Children.Add(_sheetGridGroup);

            _gridNumbersGroup = new Model3DGroup();
            sceneGroup.Children.Add(_gridNumbersGroup);

            textPointsGroup = new Model3DGroup();
            sceneGroup.Children.Add(textPointsGroup);

            this.SizeChanged += Window_SizeChanged;

            // Инициализируем трансформации
            _currentSheetTransform = _sheetTransform.GetTransform();
            _currentGridTransform = _gridTransform.GetTransform();

            InitializeScene();
            _sceneManager = new SceneObjectManager(this);

            // Подписываемся на события из XAML
            if (ApplySheetTransformButton != null)
            {
                ApplySheetTransformButton.Click += ApplySheetTransformButton_Click;
            }

            if (ResetSheetTransformButton != null)
            {
                ResetSheetTransformButton.Click += ResetSheetTransformButton_Click;
            }

            if (GridAttachedCheckBox != null)
            {
                GridAttachedCheckBox.Checked += GridAttachedCheckBox_Checked;
                GridAttachedCheckBox.Unchecked += GridAttachedCheckBox_Unchecked;
            }

            this.PreviewKeyDown += PointsOnModelWindow_PreviewKeyDown;
            this.Focusable = true;
            this.Loaded += (s, e) =>
            {
                this.Focus();
                Keyboard.Focus(this);
                UpdateCamera();
            };

            SvgRotationTextBox.Text = "0";

            TextOffsetXTextBox.Text = "0";
            TextOffsetYTextBox.Text = "0";
            TextOffsetZTextBox.Text = "50";
            TextScaleTextBox.Text = "1.0";
            TextRotationTextBox.Text = "0";
            TextPointStepTextBox.Text = "0.5";
            TextInvertXCheckBox.IsChecked = false;
            TextInvertYCheckBox.IsChecked = false;
            TextSeparateCharactersCheckBox.IsChecked = true;


            ToggleGridInversionCheckBox.IsChecked = _invertGrid;

            // Убираем вызов InitializeGridControls, если используем XAML
            // InitializeGridControls();
        }

        private TextBox _programNameTextBox;
        private TextBox _programCommentTextBox;
        private ComboBox _motionTypeComboBox;
        private TextBox _speedTextBox;



        private void ShowObjectsPanelButton_Click(object sender, RoutedEventArgs e)
        {
            var objectsPanel = new Window
            {
                Title = "Управление объектами",
                Width = 400,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this
            };

            var stackPanel = new StackPanel();

            var listBox = new ListBox();
            listBox.DisplayMemberPath = "Name";
            listBox.ItemsSource = _sceneManager.GetAllObjects();
            listBox.Height = 400;
            listBox.Margin = new Thickness(5);

            listBox.MouseDoubleClick += (s, args) =>
            {
                if (listBox.SelectedItem is SceneObject selected)
                {
                    ShowObjectEditPanel(selected);
                    objectsPanel.Close();
                }
            };

            var closeButton = new Button
            {
                Content = "Закрыть",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(5),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, args) => objectsPanel.Close();

            stackPanel.Children.Add(listBox);
            stackPanel.Children.Add(closeButton);

            objectsPanel.Content = stackPanel;
            objectsPanel.ShowDialog();
        }



        private void ResetSheetTransform()
        {
            _sheetTransform = new SheetTransform();
            _currentSheetTransform = _sheetTransform.GetTransform();
            _isSheetTransformApplied = false;

            if (_isGridAttachedToSheet)
            {
                _currentGridTransform = _currentSheetTransform;
                _isGridTransformApplied = false;
            }

            RedrawSheetAndGrid();
            UpdateGridAttachedObjects();
            UpdateStatus("Трансформации листа сброшены");
        }

        private void ApplySheetTransform(string offsetXStr, string offsetYStr, string offsetZStr,
                         string rotationStr, string scaleStr)
        {
            try
            {
                // Парсим параметры
                if (double.TryParse(offsetXStr.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetX))
                    _sheetTransform.OffsetX = offsetX;

                if (double.TryParse(offsetYStr.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetY))
                    _sheetTransform.OffsetY = offsetY;

                if (double.TryParse(offsetZStr.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetZ))
                    _sheetTransform.OffsetZ = offsetZ;

                if (double.TryParse(rotationStr.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
                    _sheetTransform.Rotation = rotation;

                if (double.TryParse(scaleStr.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                    _sheetTransform.Scale = scale;

                // Обновляем трансформацию листа
                _currentSheetTransform = _sheetTransform.GetTransform();
                _isSheetTransformApplied = true;

                // Если сетка привязана к листу, обновляем её трансформацию
                if (_isGridAttachedToSheet)
                {
                    _currentGridTransform = _currentSheetTransform;
                    _isGridTransformApplied = true;
                }

                // Перерисовываем лист и сетку
                RedrawSheetAndGrid();

                // Обновляем все объекты, привязанные к сетке с учетом инверсии
                UpdateGridAttachedObjectsWithInversion();

                UpdateStatus($"Трансформации листа применены: X={offsetX}, Y={offsetY}, Z={offsetZ}, R={rotation}, S={scale}" +
                            (_invertGrid ? " (с инверсией)" : ""));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении трансформаций листа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private void UpdateGridAttachedObjectsWithInversion()
        {
            UpdateGridAttachedObjects();
        }

        private void UpdateGridAttachedObjects()
        {
            foreach (var obj in _sceneManager.GetAllObjects())
            {
                if (obj.IsGridAttached)
                {
                    ApplyTransformationsToObject(obj);
                    UpdateObjectVisual(obj);
                }
            }
        }

        private Transform3D GetGridInversionTransform()
        {
            var invertTransform = new Transform3DGroup();
            invertTransform.Children.Add(new ScaleTransform3D(1, -1, 1));
            invertTransform.Children.Add(new TranslateTransform3D(0, A4_SHEET_MAX_Y + A4_SHEET_MIN_Y, 0));
            return invertTransform;
        }

        private Transform3D GetCoordinateCorrectionTransform()
        {
            return new RotateTransform3D(
                new AxisAngleRotation3D(new Vector3D(0, 0, 1), COORDINATE_CORRECTION_ANGLE));
        }

        /// <summary>
        /// Трансформация листа. При привязанной сетке к листу она же используется для сетки.
        /// </summary>
        private Transform3D GetAppliedSheetTransform()
        {
            if (!_isSheetTransformApplied || _currentSheetTransform == null)
                return null;
            return _currentSheetTransform;
        }

        /// <summary>
        /// Независимая трансформация сетки. Не применяется, если сетка привязана к листу
        /// (иначе трансформация листа применялась бы дважды).
        /// </summary>
        private Transform3D GetAppliedGridTransform()
        {
            if (_isGridAttachedToSheet)
                return null;
            if (!_isGridTransformApplied || _currentGridTransform == null)
                return null;
            return _currentGridTransform;
        }

        private void AppendTransform(Transform3DGroup target, Transform3D transform)
        {
            if (transform != null)
                target.Children.Add(transform);
        }

        private Point3D ApplyGridInversionToPoint(Point3D point)
        {
            if (!_invertGrid)
                return point;

            double invertedY = A4_SHEET_MAX_Y + A4_SHEET_MIN_Y - point.Y;
            return new Point3D(point.X, invertedY, point.Z);
        }

        private Point3D ReverseGridInversionOnPoint(Point3D point)
        {
            return ApplyGridInversionToPoint(point);
        }

        private Transform3DGroup BuildObjectTransformGroup(SceneObject obj)
        {
            var transformGroup = new Transform3DGroup();

            if (Math.Abs(obj.Scale - 1.0) > 0.001)
                transformGroup.Children.Add(new ScaleTransform3D(obj.Scale, obj.Scale, obj.Scale));

            if (Math.Abs(obj.Rotation) > 0.001)
            {
                transformGroup.Children.Add(new RotateTransform3D(
                    new AxisAngleRotation3D(new Vector3D(0, 0, 1), obj.Rotation)));
            }

            if (obj.IsGridAttached && _invertGrid)
                AppendTransform(transformGroup, GetGridInversionTransform());

            if (Math.Abs(obj.OffsetX) > 0.001 || Math.Abs(obj.OffsetY) > 0.001 || Math.Abs(obj.OffsetZ) > 0.001)
                transformGroup.Children.Add(new TranslateTransform3D(obj.OffsetX, obj.OffsetY, obj.OffsetZ));

            if (obj.IsGridAttached)
            {
                AppendTransform(transformGroup, GetAppliedSheetTransform());
                AppendTransform(transformGroup, GetAppliedGridTransform());
            }

            if (APPLY_COORDINATE_CORRECTION && Math.Abs(COORDINATE_CORRECTION_ANGLE) > 0.001)
                AppendTransform(transformGroup, GetCoordinateCorrectionTransform());

            return transformGroup;
        }

        private Point3D TransformSheetLocalPoint(Point3D localPoint)
        {
            var point = ApplyGridInversionToPoint(localPoint);

            var sheetTransform = GetAppliedSheetTransform();
            if (sheetTransform != null)
                point = sheetTransform.Transform(point);

            var gridTransform = GetAppliedGridTransform();
            if (gridTransform != null)
                point = gridTransform.Transform(point);

            if (APPLY_COORDINATE_CORRECTION)
                point = ApplyCoordinateCorrection(point);

            return point;
        }

        private Point3D WorldToSheetLocal(Point3D worldPoint)
        {
            var localPoint = ReverseCoordinateCorrection(worldPoint);

            var sheetTransform = GetAppliedSheetTransform();
            if (sheetTransform?.Inverse != null)
                localPoint = sheetTransform.Inverse.Transform(localPoint);

            var gridTransform = GetAppliedGridTransform();
            if (gridTransform?.Inverse != null)
                localPoint = gridTransform.Inverse.Transform(localPoint);

            return ReverseGridInversionOnPoint(localPoint);
        }

        private void ApplyTransformationsToObject(SceneObject obj)
        {
            obj.TransformGroup = BuildObjectTransformGroup(obj);

            if (obj.VisualModel != null)
                obj.VisualModel.Transform = obj.TransformGroup;
        }




        private void RedrawSheetAndGrid()
        {
            ClearSheetGeometry();

            // Рисуем лист с правильными координатами
            DrawA4SheetSurface();
            DrawA4SheetOutline();

            // Рисуем сетку если включена
            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
            }

            if (_isNumberedGridVisible)
            {
                CreateNumberedGrid();
            }
        }

        private void RedrawGridOnly()
        {
            ClearSheetGrid();
            ClearNumberedGrid();

            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
            }

            if (_isNumberedGridVisible)
            {
                CreateNumberedGrid();
            }
        }

        private void ClearSheetGeometry()
        {
            _sheetGroup.Children.Clear();
            _sheetGridGroup.Children.Clear();
            _gridNumbersGroup.Children.Clear();
        }


        private void NumberedGridCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _isNumberedGridVisible = true;
            CreateNumberedGrid();
            UpdateStatus("Сетка с номерами включена");
        }

        private void NumberedGridCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isNumberedGridVisible = false;
            ClearNumberedGrid();
            UpdateStatus("Сетка с номерами отключена");
        }

        private void CreateNumberedGrid()
        {
            ClearNumberedGrid();
            _gridCells.Clear();

            double gridZ = A4_SHEET_MIN_Z + 0.4;
            int cellNumber = 1;

            // ИНВЕРСИЯ: Нумерация СЛЕВА НАПРАВО, НО СВЕРХУ ВНИЗ (зеркально по Y)
            for (int row = 9; row >= 0; row--) // 10 строк СВЕРХУ ВНИЗ (инверсия)
            {
                for (int col = 0; col < 7; col++) // 7 колонок слева направо
                {
                    // Координаты ячейки в ЛОКАЛЬНЫХ координатах листа
                    double cellMinX = A4_SHEET_MIN_X + col * CELL_WIDTH;
                    double cellMaxX = cellMinX + CELL_WIDTH;

                    // ИНВЕРСИЯ Y: считаем сверху вниз
                    double cellMinY = A4_SHEET_MAX_Y - (row + 1) * CELL_HEIGHT;
                    double cellMaxY = cellMinY + CELL_HEIGHT;

                    // Центр ячейки в ЛОКАЛЬНЫХ координатах
                    double centerX = cellMinX + CELL_WIDTH / 2;
                    double centerY = cellMinY + CELL_HEIGHT / 2;

                    // Преобразуем в мировые координаты
                    var transformedCenter = TransformGridPoint(new Point3D(centerX, centerY, gridZ));

                    var gridCell = new GridCell
                    {
                        CellNumber = cellNumber,
                        Bounds = new Rect(cellMinX, cellMinY, CELL_WIDTH, CELL_HEIGHT),
                        Center = transformedCenter,
                        LocalCenter = new Point3D(centerX, centerY, gridZ),
                        HasObject = false
                    };

                    _gridCells.Add(gridCell);

                    // Рисуем ячейку
                    DrawGridCell(cellMinX, cellMaxX, cellMinY, cellMaxY, gridZ);

                    // Добавляем номер ячейки
                    AddCellNumber(cellNumber, centerX, centerY, gridZ);

                    cellNumber++;
                }
            }
        }






        private void DrawGridCell(double minX, double maxX, double minY, double maxY, double z)
        {
            Color cellColor = Color.FromArgb(80, 150, 150, 150);

            Point3D[] corners = new Point3D[]
            {
                TransformGridPoint(new Point3D(minX, minY, z)),
                TransformGridPoint(new Point3D(maxX, minY, z)),
                TransformGridPoint(new Point3D(maxX, maxY, z)),
                TransformGridPoint(new Point3D(minX, maxY, z))
            };

            // Рисуем линии ячейки
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                AddLineToGroup(corners[i], corners[next], cellColor, _gridNumbersGroup, 0.3);
            }
        }



        private void GridAttachedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _isGridAttachedToSheet = true;
            // При привязке сетки к листу используем трансформацию листа как основу
            _currentGridTransform = _currentSheetTransform;
            RedrawGridOnly();
            UpdateGridAttachedObjects();
            UpdateStatus("Сетка привязана к листу");
        }

        


        private void GridAttachedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isGridAttachedToSheet = false;
            // При отвязке сохраняем текущую трансформацию сетки
            if (_currentGridTransform == null)
            {
                _currentGridTransform = new Transform3DGroup();
            }
            RedrawGridOnly();
            UpdateGridAttachedObjects();
            UpdateStatus("Сетка независима от листа");
        }

        private void AddCellNumber(int number, double x, double y, double z)
        {
            var position = TransformSheetLocalPoint(new Point3D(x, y, z + 0.1));

            var sphere = new Sphere(position, 1.5, Colors.DarkBlue);
            _gridNumbersGroup.Children.Add(sphere.GetModel3D());

            AddNumberText(position, number.ToString());
        }

        private void AddNumberText(Point3D position, string text)
        {
            double offsetX = -text.Length * 0.8;

            foreach (char digit in text)
            {
                Color color;
                switch (digit)
                {
                    case '1': color = Colors.Red; break;
                    case '2': color = Colors.Green; break;
                    case '3': color = Colors.Blue; break;
                    case '4': color = Colors.Yellow; break;
                    case '5': color = Colors.Magenta; break;
                    case '6': color = Colors.Cyan; break;
                    case '7': color = Colors.Orange; break;
                    case '8': color = Colors.Purple; break;
                    case '9': color = Colors.Lime; break;
                    default: color = Colors.White; break;
                }

                var digitPos = new Point3D(position.X + offsetX, position.Y, position.Z);
                var sphere = new Sphere(digitPos, 0.6, color);
                _gridNumbersGroup.Children.Add(sphere.GetModel3D());
                offsetX += 1.2;
            }
        }

        private void ClearNumberedGrid()
        {
            _gridNumbersGroup.Children.Clear();
        }

        private GridCell FindCellByNumber(int cellNumber)
        {
            return _gridCells.FirstOrDefault(cell => cell.CellNumber == cellNumber);
        }

        private GridCell FindCellForPoint(Point3D point)
        {
            var localPoint = WorldToSheetLocal(point);

            foreach (var cell in _gridCells)
            {
                if (localPoint.X >= cell.Bounds.Left && localPoint.X <= cell.Bounds.Right &&
                    localPoint.Y >= cell.Bounds.Top && localPoint.Y <= cell.Bounds.Bottom)
                {
                    return cell;
                }
            }
            return null;
        }

        private void AssignToCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sceneManager.SelectedObject == null)
            {
                MessageBox.Show("Выберите объект для привязки к клетке.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(_assignCellTextBox.Text) || _assignCellTextBox.Text == "№ клетки")
            {
                MessageBox.Show("Введите номер клетки.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(_assignCellTextBox.Text, out int cellNumber))
            {
                MessageBox.Show("Некорректный номер клетки.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var cell = FindCellByNumber(cellNumber);
            if (cell == null)
            {
                MessageBox.Show($"Клетка с номером {cellNumber} не найдена.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cell.HasObject && cell.ObjectName != _sceneManager.SelectedObject.Name)
            {
                var result = MessageBox.Show($"Клетка {cellNumber} уже занята объектом '{cell.ObjectName}'. Заменить?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            // Если объект уже был привязан к другой клетке, освобождаем её
            if (_objectToCellMap.ContainsKey(_sceneManager.SelectedObject.Name))
            {
                int oldCellNum = _objectToCellMap[_sceneManager.SelectedObject.Name];
                _cellToObjectMap.Remove(oldCellNum);

                var oldCell = FindCellByNumber(oldCellNum);
                if (oldCell != null)
                {
                    oldCell.HasObject = false;
                    oldCell.ObjectName = null;
                }
            }

            // Обновляем привязки
            _objectToCellMap[_sceneManager.SelectedObject.Name] = cellNumber;
            _cellToObjectMap[cellNumber] = _sceneManager.SelectedObject.Name;

            cell.HasObject = true;
            cell.ObjectName = _sceneManager.SelectedObject.Name;

            // Перемещаем объект в клетку
            MoveObjectToCell(_sceneManager.SelectedObject, cell);

            // Обновляем панель редактирования
            if (_currentEditObject != null && _currentEditObject == _sceneManager.SelectedObject && _cellInfoLabel != null)
            {
                _cellInfoLabel.Text = $"Привязанная клетка: {cellNumber}";
            }

            UpdateStatus($"Объект '{_sceneManager.SelectedObject.Name}' перемещен в клетку {cellNumber}");
        }

        private void MoveObjectToCell(SceneObject obj, GridCell cell)
        {
            if (obj == null || cell == null) return;

            // ВАЖНО: Используем ЛОКАЛЬНЫЕ координаты ячейки
            var localCenter = cell.LocalCenter;

            // Устанавливаем позицию объекта в локальных координатах
            obj.OffsetX = localCenter.X;
            obj.OffsetY = localCenter.Y;
            obj.OffsetZ = 0; // Всегда Z=0 при привязке к сетке
            obj.Scale = 0.12; // Масштаб 0.12 при привязке к сетке
            obj.Rotation = 0; // Сбрасываем поворот
            obj.IsGridAttached = true;

            // Применяем трансформации
            ApplyTransformationsToObject(obj);
            UpdateObjectVisual(obj);

            // Обновляем привязки
            if (_objectToCellMap.ContainsKey(obj.Name))
            {
                int oldCellNum = _objectToCellMap[obj.Name];
                _cellToObjectMap.Remove(oldCellNum);

                var oldCell = FindCellByNumber(oldCellNum);
                if (oldCell != null)
                {
                    oldCell.HasObject = false;
                    oldCell.ObjectName = null;
                }
            }

            _objectToCellMap[obj.Name] = cell.CellNumber;
            _cellToObjectMap[cell.CellNumber] = obj.Name;
            cell.HasObject = true;
            cell.ObjectName = obj.Name;

            // Обновляем UI
            if (obj == _currentEditObject && _objectOffsetXTextBox != null)
            {
                _objectOffsetXTextBox.Text = obj.OffsetX.ToString(CultureInfo.InvariantCulture);
                _objectOffsetYTextBox.Text = obj.OffsetY.ToString(CultureInfo.InvariantCulture);
                _objectOffsetZTextBox.Text = obj.OffsetZ.ToString(CultureInfo.InvariantCulture);
                _objectScaleTextBox.Text = obj.Scale.ToString(CultureInfo.InvariantCulture);
                _objectRotationTextBox.Text = obj.Rotation.ToString(CultureInfo.InvariantCulture);

                if (_objectCellNumberTextBox != null)
                {
                    _objectCellNumberTextBox.Text = cell.CellNumber.ToString();
                }
            }

            UpdateStatus($"Объект '{obj.Name}' перемещен в клетку {cell.CellNumber} (X={obj.OffsetX:F2}, Y={obj.OffsetY:F2}, Z={obj.OffsetZ:F2})");
        }

        private bool TryPickObject(Point mousePos, out Point3D hitPoint, out SceneObject hitObject)
        {
            hitPoint = new Point3D();
            hitObject = null;

            var result = VisualTreeHelper.HitTest(Viewport3D, mousePos);
            if (result == null) return false;

            var rayMeshResult = result as RayMeshGeometry3DHitTestResult;
            if (rayMeshResult == null) return false;

            hitPoint = rayMeshResult.PointHit;

            foreach (var obj in _sceneManager.GetAllObjects())
            {
                if (obj.VisualModel == null) continue;

                if (ContainsGeometry(obj.VisualModel, rayMeshResult.VisualHit as ModelVisual3D))
                {
                    hitObject = obj;

                    if (_isNumberedGridVisible && _gridCells.Count > 0)
                    {
                        var cell = FindCellForPoint(hitPoint);
                        if (cell != null && _assignCellTextBox != null)
                        {
                            _assignCellTextBox.Text = cell.CellNumber.ToString();
                            UpdateStatus($"Объект в клетке {cell.CellNumber}");
                        }
                    }

                    return true;
                }
            }

            if (_isNumberedGridVisible && _gridCells.Count > 0)
            {
                var cell = FindCellForPoint(hitPoint);
                if (cell != null && _assignCellTextBox != null)
                {
                    _assignCellTextBox.Text = cell.CellNumber.ToString();
                    UpdateStatus($"Клетка {cell.CellNumber}" +
                    (cell.HasObject ? " (занята: " + cell.ObjectName + ")" : " (свободна)"));
                }
            }

            return false;
        }

        private bool ContainsGeometry(Model3DGroup group, ModelVisual3D visual)
        {
            if (visual == null) return false;

            return ContainsGeometryRecursive(group, visual.Content as Model3DGroup);
        }

        private bool ContainsGeometryRecursive(Model3DGroup parent, Model3D child)
        {
            if (parent == child) return true;

            foreach (var item in parent.Children)
            {
                if (item == child) return true;

                if (item is Model3DGroup childGroup)
                {
                    if (ContainsGeometryRecursive(childGroup, child))
                        return true;
                }
            }

            return false;
        }

        private void ShowObjectEditPanel(SceneObject sceneObject)
        {
            var mainControls = (Border)this.FindName("ControlsPanel");
            if (mainControls != null)
                mainControls.Visibility = Visibility.Collapsed;

            var editPanel = CreateOrGetEditPanel();
            UpdateEditPanelFields(sceneObject);

            editPanel.MaxHeight = this.ActualHeight - 100;

            if (editPanel.Child is ScrollViewer scrollViewer &&
                scrollViewer.Content is StackPanel stackPanel)
            {
                var gridGroup = FindChild<GroupBox>(stackPanel, null);
                if (gridGroup != null && gridGroup.Content is StackPanel gridStack)
                {
                    var toggleButton = FindChild<Button>(gridStack, null);
                    if (toggleButton != null)
                    {
                        toggleButton.Content = _isSheetGridVisible ? "Скрыть разметку" : "Разметка листа";
                    }

                    var numberedGridCheckBox = FindChild<CheckBox>(gridStack, null);
                    if (numberedGridCheckBox != null)
                    {
                        numberedGridCheckBox.IsChecked = _isNumberedGridVisible;
                    }

                    var gridSnapCheckBox = FindChild<CheckBox>(gridStack, null);
                    if (gridSnapCheckBox != null)
                    {
                        gridSnapCheckBox.IsChecked = _isGridSnapEnabled;
                    }
                }
            }

            editPanel.Visibility = Visibility.Visible;

            if (editPanel.Child is ScrollViewer viewer)
            {
                viewer.ScrollToVerticalOffset(0);
            }
        }

        private void UpdateEditPanelSize()
        {
            if (_objectEditPanel != null && _objectEditPanel.Visibility == Visibility.Visible)
            {
                double maxHeight = Math.Max(400, this.ActualHeight * 0.8);
                _objectEditPanel.MaxHeight = maxHeight;

                var fieldsScrollViewer = FindChild<ScrollViewer>(_objectEditPanel, null);
                if (fieldsScrollViewer != null)
                {
                    fieldsScrollViewer.MaxHeight = maxHeight * 0.4;
                }
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateEditPanelSize();
        }

        private Border CreateOrGetEditPanel()
        {
            if (_objectEditPanel != null)
                return _objectEditPanel;

            _objectEditPanel = new Border
            {
                Name = "ObjectEditPanel",
                Style = (Style)Resources["Card"],
                Margin = new Thickness(5, 10, 10, 10),
                Visibility = Visibility.Collapsed,
                VerticalAlignment = VerticalAlignment.Top,
                MaxHeight = 700
            };

            var mainScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            var mainStackPanel = new StackPanel();

            var header = new TextBlock
            {
                Text = "Редактирование объекта",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 15)
            };

            mainStackPanel.Children.Add(header);

            var gridControlGroup = new GroupBox
            {
                Header = "Управление сеткой",
                Margin = new Thickness(0, 0, 0, 15),
                Padding = new Thickness(10)
            };

            var gridControlStack = new StackPanel();

            var toggleGridButton = new Button
            {
                Content = "Разметка листа",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(0, 0, 0, 10)
            };
            toggleGridButton.Click += (s, e) => ToggleGridButton_Click(s, e);
            gridControlStack.Children.Add(toggleGridButton);

            var showNumberedGridCheckBox = new CheckBox
            {
                Content = "Показать сетку с номерами",
                Margin = new Thickness(0, 0, 0, 10),
                IsChecked = _isNumberedGridVisible
            };
            showNumberedGridCheckBox.Checked += (s, e) =>
            {
                _isNumberedGridVisible = true;
                CreateNumberedGrid();
                if (_numberedGridCheckBox != null)
                    _numberedGridCheckBox.IsChecked = true;
                UpdateStatus("Сетка с номерами включена");
            };
            showNumberedGridCheckBox.Unchecked += (s, e) =>
            {
                _isNumberedGridVisible = false;
                ClearNumberedGrid();
                if (_numberedGridCheckBox != null)
                    _numberedGridCheckBox.IsChecked = false;
                UpdateStatus("Сетка с номерами отключена");
            };
            gridControlStack.Children.Add(showNumberedGridCheckBox);

            var gridSnapCheckBox = new CheckBox
            {
                Content = "Привязка к сетке",
                Margin = new Thickness(0, 0, 0, 15),
                IsChecked = _isGridSnapEnabled
            };
            gridSnapCheckBox.Checked += (s, e) =>
            {
                _isGridSnapEnabled = true;
                if (_gridSnapCheckBox != null)
                    _gridSnapCheckBox.IsChecked = true;
                UpdateStatus("Привязка к сетке включена");
            };
            gridSnapCheckBox.Unchecked += (s, e) =>
            {
                _isGridSnapEnabled = false;
                if (_gridSnapCheckBox != null)
                    _gridSnapCheckBox.IsChecked = false;
                UpdateStatus("Привязка к сетке отключена");
            };
            gridControlStack.Children.Add(gridSnapCheckBox);

            gridControlGroup.Content = gridControlStack;
            mainStackPanel.Children.Add(gridControlGroup);

            _cellInfoLabel = new TextBlock
            {
                Text = "Привязанная клетка: нет",
                Margin = new Thickness(0, 0, 0, 15),
                FontWeight = FontWeights.Bold
            };
            mainStackPanel.Children.Add(_cellInfoLabel);

            var cellAssignGrid = new Grid();
            cellAssignGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cellAssignGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            cellAssignGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            cellAssignGrid.Margin = new Thickness(0, 0, 0, 15);

            _objectCellNumberTextBox = new TextBox
            {
                Style = (Style)Resources["ModernTextBox"],
                Text = "Номер клетки",
                Tag = "Номер клетки",
                VerticalAlignment = VerticalAlignment.Center
            };
            _objectCellNumberTextBox.GotFocus += (s, e) =>
            {
                if (_objectCellNumberTextBox.Text == "Номер клетки")
                    _objectCellNumberTextBox.Text = "";
            };
            _objectCellNumberTextBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_objectCellNumberTextBox.Text))
                    _objectCellNumberTextBox.Text = "Номер клетки";
            };
            Grid.SetColumn(_objectCellNumberTextBox, 0);

            var checkCellButton = new Button
            {
                Content = "Проверить",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(5, 0, 0, 0)
            };
            checkCellButton.Click += CheckCellButton_Click;
            Grid.SetColumn(checkCellButton, 1);

            var assignCellButton = new Button
            {
                Content = "Привязать",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(5, 0, 0, 0)
            };
            assignCellButton.Click += AssignCellButton_Click;
            Grid.SetColumn(assignCellButton, 2);

            cellAssignGrid.Children.Add(_objectCellNumberTextBox);
            cellAssignGrid.Children.Add(checkCellButton);
            cellAssignGrid.Children.Add(assignCellButton);
            mainStackPanel.Children.Add(cellAssignGrid);

            var fieldsScrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 300,
                Margin = new Thickness(0, 0, 0, 15)
            };

            var fieldsStackPanel = new StackPanel();

            _objectNameTextBox = AddEditFieldWithReference(fieldsStackPanel, "Название:", "Test Object");
            _objectOffsetXTextBox = AddEditFieldWithReference(fieldsStackPanel, "Смещение X:", "0");
            _objectOffsetYTextBox = AddEditFieldWithReference(fieldsStackPanel, "Смещение Y:", "0");
            _objectOffsetZTextBox = AddEditFieldWithReference(fieldsStackPanel, "Смещение Z:", "50");
            _objectScaleTextBox = AddEditFieldWithReference(fieldsStackPanel, "Масштаб:", "1.0");
            _objectRotationTextBox = AddEditFieldWithReference(fieldsStackPanel, "Поворот:", "0");

            fieldsScrollViewer.Content = fieldsStackPanel;
            mainStackPanel.Children.Add(fieldsScrollViewer);

            var strokeWidthGroup = new GroupBox
            {
                Header = "Параметры штриха",
                Margin = new Thickness(0, 10, 0, 15),
                Padding = new Thickness(10)
            };

            var strokeWidthStack = new StackPanel();

            var strokeWidthGrid = new Grid();
            strokeWidthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            strokeWidthGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            strokeWidthGrid.Margin = new Thickness(0, 0, 0, 10);

            var strokeWidthLabel = new TextBlock
            {
                Text = "Ширина штриха:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(strokeWidthLabel, 0);

            _objectStrokeWidthTextBox = new TextBox
            {
                Style = (Style)Resources["ModernTextBox"],
                Text = "0.2",
                VerticalAlignment = VerticalAlignment.Center
            };
            _objectStrokeWidthTextBox.PreviewTextInput += NumberValidationTextBox;
            Grid.SetColumn(_objectStrokeWidthTextBox, 1);

            strokeWidthGrid.Children.Add(strokeWidthLabel);
            strokeWidthGrid.Children.Add(_objectStrokeWidthTextBox);
            strokeWidthStack.Children.Add(strokeWidthGrid);

            var strokeWidthToolTip = new TextBlock
            {
                Text = "Вариация ширины штриха (0.1 - 2.0)\n" +
                       "0.1 = R = 0° (вертикально вниз)\n" +
                       "1.0 = R = 45° (наклонно)\n" +
                       "2.0 = R = 90° (горизонтально вправо)",
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap
            };
            _objectStrokeWidthTextBox.ToolTip = strokeWidthToolTip;

            var calligraphyModeGrid = new Grid();
            calligraphyModeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            calligraphyModeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            calligraphyModeGrid.Margin = new Thickness(0, 0, 0, 10);

            var calligraphyModeLabel = new TextBlock
            {
                Text = "Режим каллиграфии:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(calligraphyModeLabel, 0);

            _objectCalligraphyModeComboBox = new ComboBox
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            _objectCalligraphyModeComboBox.Items.Add(new ComboBoxItem { Content = "Однолинейное", Tag = "SingleStroke", IsSelected = true });
            _objectCalligraphyModeComboBox.Items.Add(new ComboBoxItem { Content = "Обводка контуром", Tag = "OutlineFill" });
            _objectCalligraphyModeComboBox.Items.Add(new ComboBoxItem { Content = "Кистевое письмо", Tag = "BrushStroke" });
            Grid.SetColumn(_objectCalligraphyModeComboBox, 1);

            calligraphyModeGrid.Children.Add(calligraphyModeLabel);
            calligraphyModeGrid.Children.Add(_objectCalligraphyModeComboBox);
            strokeWidthStack.Children.Add(calligraphyModeGrid);

            var liftHeightGrid = new Grid();
            liftHeightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            liftHeightGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            liftHeightGrid.Margin = new Thickness(0, 0, 0, 10);

            var liftHeightLabel = new TextBlock
            {
                Text = "Высота подъема:",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(liftHeightLabel, 0);

            var liftHeightTextBox = new TextBox
            {
                Style = (Style)Resources["ModernTextBox"],
                Text = "20.0",
                VerticalAlignment = VerticalAlignment.Center,
                Name = "liftHeightTextBox",
                Tag = "liftHeight"
            };

            liftHeightTextBox.PreviewTextInput += NumberValidationTextBox;
            Grid.SetColumn(liftHeightTextBox, 1);

            liftHeightGrid.Children.Add(liftHeightLabel);
            liftHeightGrid.Children.Add(liftHeightTextBox);
            strokeWidthStack.Children.Add(liftHeightGrid);

            _objectCalligraphyEnabledCheckBox = new CheckBox
            {
                Content = "Включить каллиграфический режим",
                Margin = new Thickness(0, 10, 0, 0),
                IsChecked = false
            };
            strokeWidthStack.Children.Add(_objectCalligraphyEnabledCheckBox);

            strokeWidthGroup.Content = strokeWidthStack;
            mainStackPanel.Children.Add(strokeWidthGroup);

            var objectGridAttachedCheckBox = new CheckBox
            {
                Content = "Привязать объект к разметке",
                Margin = new Thickness(0, 0, 0, 15),
                IsChecked = false
            };
            objectGridAttachedCheckBox.Checked += (s, e) =>
            {
                if (_currentEditObject != null)
                {
                    _currentEditObject.IsGridAttached = true;
                    AttachObjectToGrid(_currentEditObject);
                }
            };
            objectGridAttachedCheckBox.Unchecked += (s, e) =>
            {
                if (_currentEditObject != null)
                {
                    _currentEditObject.IsGridAttached = false;
                    DetachObjectFromGrid(_currentEditObject);
                }
            };
            mainStackPanel.Children.Add(objectGridAttachedCheckBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 15, 0, 0)
            };

            var applyButton = new Button
            {
                Content = "Применить",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(0, 0, 10, 0)
            };
            applyButton.Click += ApplyObjectChanges_Click;

            var regenerateButton = new Button
            {
                Content = "Перегенерировать",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(0, 0, 10, 0)
            };
            regenerateButton.Click += RegenerateObject_Click;

            var deleteButton = new Button
            {
                Content = "Удалить",
                Style = (Style)Resources["ModernButton"],
                Background = new SolidColorBrush(Colors.DarkRed)
            };
            deleteButton.Click += DeleteObject_Click;

            var closeButton = new Button
            {
                Content = "Закрыть",
                Style = (Style)Resources["ModernButton"],
                Margin = new Thickness(10, 0, 0, 0)
            };
            closeButton.Click += CloseEditPanel_Click;

            buttonPanel.Children.Add(applyButton);
            buttonPanel.Children.Add(regenerateButton);
            buttonPanel.Children.Add(deleteButton);
            buttonPanel.Children.Add(closeButton);

            mainStackPanel.Children.Add(buttonPanel);

            mainScrollViewer.Content = mainStackPanel;
            _objectEditPanel.Child = mainScrollViewer;

            var grid = (Grid)this.Content;
            Grid.SetColumn(_objectEditPanel, 1);
            grid.Children.Add(_objectEditPanel);

            return _objectEditPanel;
        }

        private void RegenerateObject_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditObject == null) return;

            try
            {
                ApplyObjectChanges_Click(sender, e);

                switch (_currentEditObject.Type)
                {
                    case SceneObjectType.SVG:
                    case SceneObjectType.Calligraphy:
                        LoadSvgIntoSceneObject(_currentEditObject);
                        break;

                    case SceneObjectType.Text:
                        if (!string.IsNullOrEmpty(_currentEditObject.TextContent))
                        {
                            var points = TextToPointsGenerator.CreateTextPoints(
                                text: _currentEditObject.TextContent,
                                mode: TextGenerationMode.Contour,
                                fontName: _currentEditObject.FontFamily,
                                fontSize: _currentEditObject.FontSize,
                                depth: 50,
                                scale: (float)_currentEditObject.Scale,
                                offset: new Point3D(0, 0, 0),
                                liftHeight: 10.0f,
                                pointStep: 0.5f,
                                separateCharacters: true);

                            // ВАЖНОЕ ИСПРАВЛЕНИЕ: Сохраняем исходные точки без трансформаций
                            _currentEditObject.Points.Clear();
                            _currentEditObject.Points.AddRange(points);
                        }
                        break;
                }

                UpdateObjectVisual(_currentEditObject);
                UpdateStatus($"Объект '{_currentEditObject.Name}' перегенерирован");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перегенерации объекта: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CheckCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isNumberedGridVisible)
            {
                MessageBox.Show("Сначала включите сетку с номерами.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(_objectCellNumberTextBox.Text) || _objectCellNumberTextBox.Text == "Номер клетки")
            {
                MessageBox.Show("Введите номер клетки для проверки.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(_objectCellNumberTextBox.Text, out int cellNumber))
            {
                MessageBox.Show("Некорректный номер клетки.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var cell = FindCellByNumber(cellNumber);
            if (cell == null)
            {
                MessageBox.Show($"Клетка с номером {cellNumber} не найдена.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                if (cell.HasObject)
                {
                    MessageBox.Show($"Клетка {cellNumber} занята объектом: {cell.ObjectName}", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"Клетка {cellNumber} свободна.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void AssignCellButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditObject == null)
            {
                MessageBox.Show("Нет объекта для привязки.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!_isNumberedGridVisible)
            {
                MessageBox.Show("Сначала включите сетку с номерами.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(_objectCellNumberTextBox.Text) || _objectCellNumberTextBox.Text == "Номер клетки")
            {
                MessageBox.Show("Введите номер клетки.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!int.TryParse(_objectCellNumberTextBox.Text, out int cellNumber))
            {
                MessageBox.Show("Некорректный номер клетки.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var cell = FindCellByNumber(cellNumber);
            if (cell == null)
            {
                MessageBox.Show($"Клетка с номером {cellNumber} не найдена.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (cell.HasObject && cell.ObjectName != _currentEditObject.Name)
            {
                var result = MessageBox.Show($"Клетка {cellNumber} уже занята объектом '{cell.ObjectName}'. Заменить?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                    return;
            }

            if (_objectToCellMap.ContainsKey(_currentEditObject.Name))
            {
                int oldCellNum = _objectToCellMap[_currentEditObject.Name];
                _cellToObjectMap.Remove(oldCellNum);

                var oldCell = FindCellByNumber(oldCellNum);
                if (oldCell != null)
                {
                    oldCell.HasObject = false;
                    oldCell.ObjectName = null;
                }
            }

            _objectToCellMap[_currentEditObject.Name] = cellNumber;
            _cellToObjectMap[cellNumber] = _currentEditObject.Name;

            cell.HasObject = true;
            cell.ObjectName = _currentEditObject.Name;

            MoveObjectToCell(_currentEditObject, cell);

            if (_cellInfoLabel != null)
            {
                _cellInfoLabel.Text = $"Привязанная клетка: {cellNumber}";
            }

            UpdateStatus($"Объект '{_currentEditObject.Name}' перемещен в клетку {cellNumber} (Z=0, масштаб=0.12)");
        }

        private TextBox AddEditFieldWithReference(StackPanel parent, string label, string defaultValue)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelCtrl = new TextBlock
            {
                Text = label,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 5, 0)
            };
            Grid.SetColumn(labelCtrl, 0);

            var textBox = new TextBox
            {
                Text = defaultValue,
                Style = (Style)Resources["ModernTextBox"],
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetColumn(textBox, 1);

            grid.Children.Add(labelCtrl);
            grid.Children.Add(textBox);

            if (parent != null)
            {
                parent.Children.Add(grid);
            }

            return textBox;
        }

        private void UpdateEditPanelFields(SceneObject obj)
        {
            if (_objectEditPanel == null) return;

            _currentEditObject = obj;

            _objectNameTextBox.Text = obj.Name ?? "Без названия";
            _objectOffsetXTextBox.Text = obj.OffsetX.ToString(CultureInfo.InvariantCulture);
            _objectOffsetYTextBox.Text = obj.OffsetY.ToString(CultureInfo.InvariantCulture);
            _objectOffsetZTextBox.Text = obj.OffsetZ.ToString(CultureInfo.InvariantCulture);
            _objectScaleTextBox.Text = obj.Scale.ToString(CultureInfo.InvariantCulture);
            _objectRotationTextBox.Text = obj.Rotation.ToString(CultureInfo.InvariantCulture);

            if (_objectToCellMap.ContainsKey(obj.Name))
            {
                int cellNumber = _objectToCellMap[obj.Name];
                _cellInfoLabel.Text = $"Привязанная клетка: {cellNumber}";
                _objectCellNumberTextBox.Text = cellNumber.ToString();
            }
            else
            {
                _cellInfoLabel.Text = "Привязанная клетка: нет";
                _objectCellNumberTextBox.Text = "Номер клетки";
            }

            if (_objectStrokeWidthTextBox != null)
            {
                _objectStrokeWidthTextBox.Text = obj.StrokeWidth.ToString("F2", CultureInfo.InvariantCulture);
            }

            if (_objectCalligraphyEnabledCheckBox != null)
            {
                _objectCalligraphyEnabledCheckBox.IsChecked = obj.Type == SceneObjectType.Calligraphy;
            }

            if (_objectCalligraphyModeComboBox != null && obj.Type == SceneObjectType.Calligraphy)
            {
                foreach (ComboBoxItem item in _objectCalligraphyModeComboBox.Items)
                {
                    if (item.Tag?.ToString() == obj.CalligraphyMode.ToString())
                    {
                        _objectCalligraphyModeComboBox.SelectedItem = item;
                        break;
                    }
                }
            }

            var gridAttachedCheckBox = FindChild<CheckBox>(_objectEditPanel, null);
            if (gridAttachedCheckBox != null)
            {
                gridAttachedCheckBox.IsChecked = obj.IsGridAttached;
            }
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T && (string.IsNullOrEmpty(childName) || ((FrameworkElement)child).Name == childName))
                    return (T)child;

                var result = FindChild<T>(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void ApplyObjectChanges_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditObject == null) return;

            try
            {
                _currentEditObject.Name = _objectNameTextBox.Text;

                if (double.TryParse(_objectOffsetXTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double ox))
                    _currentEditObject.OffsetX = ox;

                if (double.TryParse(_objectOffsetYTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double oy))
                    _currentEditObject.OffsetY = oy;

                if (double.TryParse(_objectOffsetZTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double oz))
                    _currentEditObject.OffsetZ = oz;

                if (double.TryParse(_objectScaleTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                    _currentEditObject.Scale = scale;

                if (double.TryParse(_objectRotationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rot))
                    _currentEditObject.Rotation = rot;

                if (_objectStrokeWidthTextBox != null &&
                    double.TryParse(_objectStrokeWidthTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double strokeWidth))
                {
                    _currentEditObject.StrokeWidth = strokeWidth;
                }

                if (_objectCalligraphyModeComboBox != null &&
                    _objectCalligraphyModeComboBox.SelectedItem is ComboBoxItem selectedMode)
                {
                    if (Enum.TryParse<CalligraphyMode>(selectedMode.Tag.ToString(), out CalligraphyMode mode))
                    {
                        _currentEditObject.CalligraphyMode = mode;
                    }
                }

                if (_currentEditObject.IsGridAttached && _isGridSnapEnabled)
                {
                    SnapObjectToGrid(_currentEditObject);
                }

                if (_objectToCellMap.ContainsKey(_currentEditObject.Name))
                {
                    int cellNumber = _objectToCellMap[_currentEditObject.Name];
                    var cell = FindCellByNumber(cellNumber);
                    if (cell != null)
                    {
                        if (_currentEditObject.OffsetX < cell.Bounds.Left ||
                            _currentEditObject.OffsetX > cell.Bounds.Right ||
                            _currentEditObject.OffsetY < cell.Bounds.Top ||
                            _currentEditObject.OffsetY > cell.Bounds.Bottom)
                        {
                            _objectToCellMap.Remove(_currentEditObject.Name);
                            _cellToObjectMap.Remove(cellNumber);
                            cell.HasObject = false;
                            cell.ObjectName = null;

                            if (_cellInfoLabel != null)
                            {
                                _cellInfoLabel.Text = "Привязанная клетка: нет";
                            }

                            UpdateStatus($"Объект '{_currentEditObject.Name}' отвязан от клетки {cellNumber}");
                        }
                    }
                }

                ApplyTransformationsToObject(_currentEditObject);
                UpdateObjectVisual(_currentEditObject);

                UpdateStatus($"Объект '{_currentEditObject.Name}' обновлен");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении изменений: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteObject_Click(object sender, RoutedEventArgs e)
        {
            if (_currentEditObject != null)
            {
                var result = MessageBox.Show($"Вы уверены, что хотите удалить объект '{_currentEditObject.Name}'?",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    if (_objectToCellMap.ContainsKey(_currentEditObject.Name))
                    {
                        int cellNumber = _objectToCellMap[_currentEditObject.Name];
                        _cellToObjectMap.Remove(cellNumber);
                        _objectToCellMap.Remove(_currentEditObject.Name);

                        var cell = FindCellByNumber(cellNumber);
                        if (cell != null)
                        {
                            cell.HasObject = false;
                            cell.ObjectName = null;
                        }
                    }

                    if (_currentEditObject.IsGridAttached)
                    {
                        DetachObjectFromGrid(_currentEditObject);
                    }
                    _sceneManager.RemoveObject(_currentEditObject.Id);
                    CloseEditPanel_Click(sender, e);
                }
            }
        }

        private void CloseEditPanel_Click(object sender, RoutedEventArgs e)
        {
            if (_objectEditPanel != null)
                _objectEditPanel.Visibility = Visibility.Collapsed;

            var mainControls = (Border)this.FindName("ControlsPanel");
            if (mainControls != null)
                mainControls.Visibility = Visibility.Visible;

            _currentEditObject = null;
            _sceneManager.Deselect();
        }

        private void ToggleGridButton_Click(object sender, RoutedEventArgs e)
        {
            _isSheetGridVisible = !_isSheetGridVisible;

            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
                if (_toggleGridButton != null)
                    _toggleGridButton.Content = "Скрыть разметку";
                UpdateStatus("Разметка листа включена");
            }
            else
            {
                ClearSheetGrid();
                if (_toggleGridButton != null)
                    _toggleGridButton.Content = "Разметка листа";
                UpdateStatus("Разметка листа отключена");
            }
        }

        private void GridSnapCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _isGridSnapEnabled = true;
            UpdateStatus("Привязка к сетке включена");
        }

        private void GridSnapCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _isGridSnapEnabled = false;
            UpdateStatus("Привязка к сетке отключена");
        }

        private void DrawSheetGrid()
        {
            Color gridColor = Color.FromArgb(100, 200, 200, 200);
            Color thickGridColor = Color.FromArgb(150, 150, 150, 150);
            double gridZ = A4_SHEET_MIN_Z + 0.3;

            // Вертикальные линии (7 колонок)
            for (int col = 0; col <= 7; col++)
            {
                double x = A4_SHEET_MIN_X + col * CELL_WIDTH;
                Point3D start = new Point3D(x, A4_SHEET_MIN_Y, gridZ);
                Point3D end = new Point3D(x, A4_SHEET_MAX_Y, gridZ);

                var transformedStart = TransformGridPoint(start);
                var transformedEnd = TransformGridPoint(end);

                AddLineToGroup(transformedStart, transformedEnd, gridColor, _sheetGridGroup, 0.2);
            }

            // Горизонтальные линии (10 строк) - ИНВЕРСИЯ
            for (int row = 0; row <= 10; row++)
            {
                double y;
                if (_invertGrid)
                {
                    // ИНВЕРСИЯ: считаем сверху вниз
                    y = A4_SHEET_MAX_Y - row * CELL_HEIGHT;
                }
                else
                {
                    y = A4_SHEET_MIN_Y + row * CELL_HEIGHT;
                }

                Point3D start = new Point3D(A4_SHEET_MIN_X, y, gridZ);
                Point3D end = new Point3D(A4_SHEET_MAX_X, y, gridZ);

                var transformedStart = TransformGridPoint(start);
                var transformedEnd = TransformGridPoint(end);

                AddLineToGroup(transformedStart, transformedEnd, gridColor, _sheetGridGroup, 0.2);
            }

            // Толстые линии для границ групп
            for (int col = 0; col <= 7; col += 3)
            {
                double x = A4_SHEET_MIN_X + col * CELL_WIDTH;
                if (x > A4_SHEET_MAX_X + 0.1) break;

                Point3D start = new Point3D(x, A4_SHEET_MIN_Y, gridZ);
                Point3D end = new Point3D(x, A4_SHEET_MAX_Y, gridZ);

                var transformedStart = TransformGridPoint(start);
                var transformedEnd = TransformGridPoint(end);

                AddLineToGroup(transformedStart, transformedEnd, thickGridColor, _sheetGridGroup, 0.5);
            }

            for (int row = 0; row <= 10; row += 5)
            {
                double y;
                if (_invertGrid)
                {
                    // ИНВЕРСИЯ: считаем сверху вниз
                    y = A4_SHEET_MAX_Y - row * CELL_HEIGHT;
                }
                else
                {
                    y = A4_SHEET_MIN_Y + row * CELL_HEIGHT;
                }

                if (y < A4_SHEET_MIN_Y - 0.1) break;

                Point3D start = new Point3D(A4_SHEET_MIN_X, y, gridZ);
                Point3D end = new Point3D(A4_SHEET_MAX_X, y, gridZ);

                var transformedStart = TransformGridPoint(start);
                var transformedEnd = TransformGridPoint(end);

                AddLineToGroup(transformedStart, transformedEnd, thickGridColor, _sheetGridGroup, 0.5);
            }

            AddGridLabels();
        }


        private Point3D TransformGridPoint(Point3D point)
        {
            return TransformSheetLocalPoint(point);
        }

        private Point3D TransformSheetPoint(Point3D point)
        {
            return TransformSheetLocalPoint(point);
        }

        private void AddGridLabels()
        {
            double labelZ = A4_SHEET_MIN_Z + 0.5;
            Color labelColor = Colors.DarkGray;

            // Подписи по оси Y (слева и справа) - с ИНВЕРСИЕЙ
            for (int i = 0; i <= 10; i++)
            {
                double y;
                if (_invertGrid)
                {
                    // ИНВЕРСИЯ: считаем сверху вниз
                    y = A4_SHEET_MAX_Y - i * CELL_HEIGHT;
                }
                else
                {
                    y = A4_SHEET_MIN_Y + i * CELL_HEIGHT;
                }

                string labelText;
                if (_invertGrid)
                {
                    // Для инверсной сетки подписываем от 90 до -200
                    labelText = $"{A4_SHEET_MAX_Y - i * CELL_HEIGHT:F0}";
                }
                else
                {
                    labelText = $"{A4_SHEET_MIN_Y + i * CELL_HEIGHT:F0}";
                }

                // Подписи слева
                Point3D leftLabelPos = new Point3D(A4_SHEET_MIN_X - 15, y, labelZ);
                var transformedLeftPos = TransformGridPoint(leftLabelPos);
                AddTextLabel(transformedLeftPos, labelText, labelColor, _sheetGridGroup, 3.0);

                // Подписи справа
                Point3D rightLabelPos = new Point3D(A4_SHEET_MAX_X + 5, y, labelZ);
                var transformedRightPos = TransformGridPoint(rightLabelPos);
                AddTextLabel(transformedRightPos, labelText, labelColor, _sheetGridGroup, 3.0);
            }

            // Подписи по оси X (сверху и снизу)
            for (int i = 0; i <= 7; i++)
            {
                double x = A4_SHEET_MIN_X + i * CELL_WIDTH;
                string labelText = $"{A4_SHEET_MIN_X + i * CELL_WIDTH:F0}";

                // Подписи снизу
                Point3D bottomLabelPos = new Point3D(x, A4_SHEET_MIN_Y - 20, labelZ);
                var transformedBottomPos = TransformGridPoint(bottomLabelPos);
                AddTextLabel(transformedBottomPos, labelText, labelColor, _sheetGridGroup, 3.0);

                // Подписи сверху
                Point3D topLabelPos = new Point3D(x, A4_SHEET_MAX_Y + 5, labelZ);
                var transformedTopPos = TransformGridPoint(topLabelPos);
                AddTextLabel(transformedTopPos, labelText, labelColor, _sheetGridGroup, 3.0);
            }
        }

        private void AddTextLabel(Point3D position, string text, Color color, Model3DGroup group, double size)
        {
            var sphere = new Sphere(position, size, color);
            group.Children.Add(sphere.GetModel3D());

            var lineStart = new Point3D(position.X - size * 2, position.Y, position.Z);
            var lineEnd = new Point3D(position.X + size * 2, position.Y, position.Z);
            AddLineToGroup(lineStart, lineEnd, color, group, 0.3);
        }

        private void ClearSheetGrid()
        {
            _sheetGridGroup.Children.Clear();
        }

        private void AttachObjectToGrid(SceneObject obj)
        {
            if (!_gridAttachedObjects.Contains(obj))
            {
                _gridAttachedObjects.Add(obj);

                if (_isGridSnapEnabled)
                {
                    SnapObjectToGrid(obj);
                }

                UpdateStatus($"Объект '{obj.Name}' привязан к разметке");
            }
        }

        private void DetachObjectFromGrid(SceneObject obj)
        {
            if (_gridAttachedObjects.Contains(obj))
            {
                _gridAttachedObjects.Remove(obj);
                UpdateStatus($"Объект '{obj.Name}' отвязан от разметки");
            }
        }

        private void SnapObjectToGrid(SceneObject obj)
        {
            double snappedX = Math.Round(obj.OffsetX / GRID_STEP) * GRID_STEP;
            double snappedY = Math.Round(obj.OffsetY / GRID_STEP) * GRID_STEP;

            snappedX = Math.Max(A4_SHEET_MIN_X, Math.Min(A4_SHEET_MAX_X, snappedX));
            snappedY = Math.Max(A4_SHEET_MIN_Y, Math.Min(A4_SHEET_MAX_Y, snappedY));

            obj.OffsetX = snappedX;
            obj.OffsetY = snappedY;

            if (obj == _currentEditObject && _objectOffsetXTextBox != null)
            {
                _objectOffsetXTextBox.Text = snappedX.ToString(CultureInfo.InvariantCulture);
                _objectOffsetYTextBox.Text = snappedY.ToString(CultureInfo.InvariantCulture);
            }

            ApplyTransformationsToObject(obj);
            UpdateObjectVisual(obj);
        }

        private void AddLineToGroup(Point3D start, Point3D end, Color color, Model3DGroup targetGroup, double thickness = 0.5)
        {
            Vector3D direction = end - start;
            direction.Normalize();

            MeshGeometry3D mesh = new MeshGeometry3D();

            Vector3D up = new Vector3D(0, 0, 1);
            if (Math.Abs(Vector3D.DotProduct(direction, up)) > 0.9)
                up = new Vector3D(1, 0, 0);

            Vector3D right = Vector3D.CrossProduct(direction, up);
            right.Normalize();
            Vector3D actualUp = Vector3D.CrossProduct(right, direction);
            actualUp.Normalize();

            Point3D p1 = start + right * thickness + actualUp * thickness;
            Point3D p2 = start + right * thickness - actualUp * thickness;
            Point3D p3 = start - right * thickness - actualUp * thickness;
            Point3D p4 = start - right * thickness + actualUp * thickness;
            Point3D p5 = end + right * thickness + actualUp * thickness;
            Point3D p6 = end + right * thickness - actualUp * thickness;
            Point3D p7 = end - right * thickness - actualUp * thickness;
            Point3D p8 = end - right * thickness + actualUp * thickness;

            mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3); mesh.Positions.Add(p4);
            mesh.Positions.Add(p5); mesh.Positions.Add(p6); mesh.Positions.Add(p7); mesh.Positions.Add(p8);

            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(4);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Geometry = mesh;
            geometry.Material = new DiffuseMaterial(new SolidColorBrush(color));

            Model3DGroup lineGroup = new Model3DGroup();
            lineGroup.Children.Add(geometry);

            targetGroup.Children.Add(lineGroup);
        }

        private void Viewport3D_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var clickTime = DateTime.Now;
            var diff = clickTime - _lastClickTime;

            if (diff.TotalMilliseconds < DOUBLE_CLICK_MS && e.ClickCount >= 2)
            {
                var mousePos = e.GetPosition(Viewport3D);
                if (TryPickObject(mousePos, out _, out var hitObject))
                {
                    ShowObjectEditPanel(hitObject);
                }
                _lastClickTime = clickTime;
                return;
            }

            _lastClickTime = clickTime;

            var element = sender as UIElement;
            if (element != null)
            {
                isLeftMouseButtonPressed = true;
                lastMousePosition = e.GetPosition(element);
                element.CaptureMouse();
            }
        }

        private void Viewport3D_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isLeftMouseButtonPressed = false;
                element.ReleaseMouseCapture();
            }
        }

        private void Viewport3D_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isRightMouseButtonPressed = true;
                lastMousePosition = e.GetPosition(element);
                element.CaptureMouse();
            }
        }

        private void Viewport3D_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as UIElement;
            if (element != null)
            {
                isRightMouseButtonPressed = false;
                element.ReleaseMouseCapture();
            }
        }

        private void Viewport3D_MouseMove(object sender, MouseEventArgs e)
        {
            var element = sender as UIElement;
            if (element == null) return;

            Point currentPosition = e.GetPosition(element);
            double deltaX = currentPosition.X - lastMousePosition.X;
            double deltaY = currentPosition.Y - lastMousePosition.Y;

            if (isRightMouseButtonPressed)
            {
                angleY += deltaX * MOUSE_ROTATION_SPEED;
                angleX -= deltaY * MOUSE_ROTATION_SPEED;

                if (angleX > 360) angleX -= 360;
                if (angleX < 0) angleX += 360;
                if (angleY > 360) angleY -= 360;
                if (angleY < 0) angleY += 360;

                RotationXSlider.Value = angleX;
                RotationYSlider.Value = angleY;

                UpdateCamera();
            }
            else if (isLeftMouseButtonPressed)
            {
                double radX = angleX * Math.PI / 180.0;
                double radY = angleY * Math.PI / 180.0;

                Vector3D lookDirection = new Vector3D(
                    Math.Sin(radY) * Math.Cos(radX),
                    Math.Sin(radX),
                    Math.Cos(radY) * Math.Cos(radX)
                );
                lookDirection.Normalize();

                Vector3D upVector = new Vector3D(0, 1, 0);
                Vector3D rightVector = Vector3D.CrossProduct(lookDirection, upVector);
                rightVector.Normalize();

                Vector3D actualUpVector = Vector3D.CrossProduct(rightVector, lookDirection);
                actualUpVector.Normalize();

                cameraTarget.X += (rightVector.X * deltaX + actualUpVector.X * deltaY) * MOUSE_PAN_SPEED;
                cameraTarget.Y += (rightVector.Y * deltaX + actualUpVector.Y * deltaY) * MOUSE_PAN_SPEED;
                cameraTarget.Z += (rightVector.Z * deltaX + actualUpVector.Z * deltaY) * MOUSE_PAN_SPEED;

                UpdateCamera();
            }

            lastMousePosition = currentPosition;
        }

        private void Viewport3D_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double zoomFactor = 1.0 - e.Delta * MOUSE_ZOOM_SPEED;
            cameraDistance *= zoomFactor;

            if (cameraDistance < 50) cameraDistance = 50;
            if (cameraDistance > 3000) cameraDistance = 3000;

            UpdateCamera();
            e.Handled = true;
        }

        private void InitializeScene()
        {
            // Отрисовываем лист с центром в (0,0)
            RedrawSheetAndGrid();

            // Отрисовываем оси в центре (0,0)
            DrawAxes();

            // Отрисовываем нулевую плоскость
            DrawZeroPlane();
        }

        private void DrawAxes()
        {
            // Оси рисуются из центра (0,0)
            AddLine(new Point3D(0, 0, 0), new Point3D(100, 0, 0), Colors.Red);
            AddLine(new Point3D(0, 0, 0), new Point3D(0, 100, 0), Colors.Green);
            AddLine(new Point3D(0, 0, 0), new Point3D(0, 0, 100), Colors.Blue);

            // Подписи осей
            AddTextLabel(new Point3D(110, 0, 0), "X", Colors.Red, sceneGroup, 5.0);
            AddTextLabel(new Point3D(0, 110, 0), "Y", Colors.Green, sceneGroup, 5.0);
            AddTextLabel(new Point3D(0, 0, 110), "Z", Colors.Blue, sceneGroup, 5.0);
        }

        private void DrawZeroPlane()
        {
            float gridSize = 150.0f; // Уменьшаем размер для лучшего обзора
            float gridStep = 50.0f;

            MeshGeometry3D planeMesh = new MeshGeometry3D();
            // Плоскость Z=0, центрированная в (0,0)
            planeMesh.Positions.Add(new Point3D(-gridSize, -gridSize, 0));
            planeMesh.Positions.Add(new Point3D(gridSize, -gridSize, 0));
            planeMesh.Positions.Add(new Point3D(gridSize, gridSize, 0));
            planeMesh.Positions.Add(new Point3D(-gridSize, gridSize, 0));
            planeMesh.TriangleIndices.Add(0);
            planeMesh.TriangleIndices.Add(1);
            planeMesh.TriangleIndices.Add(2);
            planeMesh.TriangleIndices.Add(0);
            planeMesh.TriangleIndices.Add(2);
            planeMesh.TriangleIndices.Add(3);

            GeometryModel3D planeGeometry = new GeometryModel3D();
            planeGeometry.Geometry = planeMesh;
            Color planeColor = Color.FromArgb(60, 100, 100, 150); // Полупрозрачный
            planeGeometry.Material = new DiffuseMaterial(new SolidColorBrush(planeColor));
            planeGeometry.BackMaterial = new DiffuseMaterial(new SolidColorBrush(planeColor));

            Model3DGroup planeGroup = new Model3DGroup();
            planeGroup.Children.Add(planeGeometry);
            sceneGroup.Children.Add(planeGroup);

            // Сетка на нулевой плоскости
            Color gridColor = Colors.DarkGray;
            for (float x = -gridSize; x <= gridSize; x += gridStep)
            {
                AddLine(new Point3D(x, -gridSize, 0.1), new Point3D(x, gridSize, 0.1), gridColor);
            }
            for (float y = -gridSize; y <= gridSize; y += gridStep)
            {
                AddLine(new Point3D(-gridSize, y, 0.1), new Point3D(gridSize, y, 0.1), gridColor);
            }
        }

        private void ToggleGridInversion()
        {
            _invertGrid = !_invertGrid;

            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
            }

            if (_isNumberedGridVisible)
            {
                CreateNumberedGrid();
            }

            UpdateStatus($"Инверсия сетки: {(_invertGrid ? "ВКЛ" : "ВЫКЛ")}");
        }

        private void DrawA4SheetSurface()
        {
            MeshGeometry3D sheetMesh = new MeshGeometry3D();

            // Углы листа в правильном порядке
            Point3D[] corners = new Point3D[]
            {
                new Point3D(A4_SHEET_MIN_X, A4_SHEET_MIN_Y, A4_SHEET_MIN_Z), // Левый нижний
                new Point3D(A4_SHEET_MAX_X, A4_SHEET_MIN_Y, A4_SHEET_MIN_Z), // Правый нижний
                new Point3D(A4_SHEET_MAX_X, A4_SHEET_MAX_Y, A4_SHEET_MIN_Z), // Правый верхний
                new Point3D(A4_SHEET_MIN_X, A4_SHEET_MAX_Y, A4_SHEET_MIN_Z)  // Левый верхний
            };

            // Применяем коррекцию координат и трансформацию листа
            foreach (var corner in corners)
            {
                var transformedCorner = TransformSheetPoint(corner);
                sheetMesh.Positions.Add(transformedCorner);
            }

            sheetMesh.TriangleIndices.Add(0);
            sheetMesh.TriangleIndices.Add(1);
            sheetMesh.TriangleIndices.Add(2);
            sheetMesh.TriangleIndices.Add(0);
            sheetMesh.TriangleIndices.Add(2);
            sheetMesh.TriangleIndices.Add(3);

            GeometryModel3D sheetGeometry = new GeometryModel3D();
            sheetGeometry.Geometry = sheetMesh;
            Color sheetSurfaceColor = Color.FromArgb(60, 100, 200, 255);
            sheetGeometry.Material = new DiffuseMaterial(new SolidColorBrush(sheetSurfaceColor));
            sheetGeometry.BackMaterial = new DiffuseMaterial(new SolidColorBrush(sheetSurfaceColor));

            _sheetGroup.Children.Add(sheetGeometry);
        }

        private void DrawA4SheetOutline()
        {
            Color outlineColor = Color.FromArgb(200, 0, 150, 200);

            Point3D[] corners = new Point3D[]
            {
                new Point3D(A4_SHEET_MIN_X, A4_SHEET_MIN_Y, A4_SHEET_MIN_Z + 0.1),
                new Point3D(A4_SHEET_MAX_X, A4_SHEET_MIN_Y, A4_SHEET_MIN_Z + 0.1),
                new Point3D(A4_SHEET_MAX_X, A4_SHEET_MAX_Y, A4_SHEET_MIN_Z + 0.1),
                new Point3D(A4_SHEET_MIN_X, A4_SHEET_MAX_Y, A4_SHEET_MIN_Z + 0.1)
            };

            // Преобразуем углы
            Point3D[] transformedCorners = new Point3D[4];
            for (int i = 0; i < 4; i++)
            {
                transformedCorners[i] = TransformSheetPoint(corners[i]);
            }

            // Рисуем контур
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                AddLineToGroup(transformedCorners[i], transformedCorners[next], outlineColor, _sheetGroup, 0.5);
            }
        }

        private void AddLine(Point3D start, Point3D end, Color color)
        {
            // Используйте уже существующий метод AddLineToGroup
            AddLineToGroup(start, end, color, sceneGroup);
        }
        private void RotationXSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            angleX = e.NewValue;
            UpdateCamera();
        }

        private void RotationYSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            angleY = e.NewValue;
            UpdateCamera();
        }

        private Model3DGroup LoadModelByExtension(string filePath)
        {
            var ext = IO_Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".fbx":
                    return LoadWithAssimp(filePath);
                default:
                    throw new NotSupportedException($"Формат файла '{ext}' не поддерживается.");
            }
        }

        private void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "3D модели FBX (*.fbx)|*.fbx|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var model = LoadModelByExtension(dialog.FileName);
                    if (model != null)
                    {
                        var sceneObject = new SceneObject
                        {
                            Type = SceneObjectType.Model3D,
                            Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                            FilePath = dialog.FileName,
                            VisualModel = model
                        };

                        var transformGroup = new Transform3DGroup();
                        var translation = new TranslateTransform3D(0, 0, 0);
                        transformGroup.Children.Add(translation);
                        model.Transform = transformGroup;

                        sceneObject.TransformGroup = transformGroup;

                        _sceneManager.AddObject(sceneObject);

                        loadedModels.Add(model);
                        lastLoadedModel = model;
                        lastLoadedModelTranslation = translation;

                        sceneGroup.Children.Add(model);

                        UpdateStatus($"3D модель загружена как объект: {sceneObject.Name}");
                    }
                }
                catch (NotSupportedException ex)
                {
                    MessageBox.Show(ex.Message, "Формат не поддерживается", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при загрузке 3D модели: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private Point3D TransformModelPoint(Point3D p)
        {
            if (lastLoadedModelTranslation == null)
                return p;

            return new Point3D(
                p.X + lastLoadedModelTranslation.OffsetX,
                p.Y + lastLoadedModelTranslation.OffsetY,
                p.Z + lastLoadedModelTranslation.OffsetZ
            );
        }

        private bool IsPointInWorkZoneWorld(Point3D p)
        {
            var wp = TransformModelPoint(p);

            return wp.X >= WORK_ZONE_MIN_X && wp.X <= WORK_ZONE_MAX_X &&
                   wp.Y >= WORK_ZONE_MIN_Y && wp.Y <= WORK_ZONE_MAX_Y &&
                   wp.Z >= WORK_ZONE_MIN_Z && wp.Z <= WORK_ZONE_MAX_Z;
        }

        private void UpdateCamera()
        {
            double radX = angleX * Math.PI / 180.0;
            double radY = angleY * Math.PI / 180.0;

            double x = cameraTarget.X + cameraDistance * Math.Sin(radY) * Math.Cos(radX);
            double y = cameraTarget.Y + cameraDistance * Math.Sin(radX);
            double z = cameraTarget.Z + cameraDistance * Math.Cos(radY) * Math.Cos(radX);

            var newPosition = new Point3D(x, y, z);
            Camera.Position = newPosition;
            Camera.LookDirection = new Vector3D(cameraTarget.X - newPosition.X,
                                                cameraTarget.Y - newPosition.Y,
                                                cameraTarget.Z - newPosition.Z);

            if (CameraPositionText != null)
            {
                CameraPositionText.Text = $"X={newPosition.X:F1}, Y={newPosition.Y:F1}, Z={newPosition.Z:F1}";
            }
        }

        private void PointsOnModelWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox)
                return;

            double moveSpeed = 20.0;
            bool handled = false;

            switch (e.Key)
            {
                case Key.W:
                    cameraTarget.Z -= moveSpeed;
                    handled = true;
                    break;
                case Key.S:
                    cameraTarget.Z += moveSpeed;
                    handled = true;
                    break;
                case Key.A:
                    cameraTarget.X -= moveSpeed;
                    handled = true;
                    break;
                case Key.D:
                    cameraTarget.X += moveSpeed;
                    handled = true;
                    break;
                case Key.Up:
                    angleX -= 5.0;
                    RotationXSlider.Value = angleX;
                    handled = true;
                    break;
                case Key.Down:
                    angleX += 5.0;
                    RotationXSlider.Value = angleX;
                    handled = true;
                    break;
                case Key.Left:
                    angleY -= 5.0;
                    RotationYSlider.Value = angleY;
                    handled = true;
                    break;
                case Key.Right:
                    angleY += 5.0;
                    RotationYSlider.Value = angleY;
                    handled = true;
                    break;
            }

            if (handled)
            {
                UpdateCamera();
                e.Handled = true;
            }
        }

        private Model3DGroup LoadWithAssimp(string filePath)
        {
            var context = new Assimp.AssimpContext();

            modelPoints.Clear();

            var attempts = new[]
            {
                Assimp.PostProcessSteps.Triangulate |
                Assimp.PostProcessSteps.JoinIdenticalVertices |
                Assimp.PostProcessSteps.GenerateNormals |
                Assimp.PostProcessSteps.ImproveCacheLocality |
                Assimp.PostProcessSteps.PreTransformVertices |
                Assimp.PostProcessSteps.OptimizeMeshes |
                Assimp.PostProcessSteps.ValidateDataStructure,

                Assimp.PostProcessSteps.Triangulate |
                Assimp.PostProcessSteps.PreTransformVertices |
                Assimp.PostProcessSteps.GenerateNormals,

                Assimp.PostProcessSteps.Triangulate
            };

            Assimp.Scene scene = null;
            foreach (var flags in attempts)
            {
                try
                {
                    scene = context.ImportFile(filePath, flags);
                    if (scene != null && scene.MeshCount > 0) break;
                }
                catch
                {
                    scene = null;
                }
            }

            if (scene == null || scene.MeshCount == 0)
            {
                MessageBox.Show(
                    "Файл FBX не содержит пригодную для отображения геометрию.\n\n" +
                    "Возможные причины и решения:\n" +
                    "- FBX содержит только кривые/сплайны или 2D-контур без faces. Конвертируйте в Mesh в Blender/3dsMax (Object -> Convert to Mesh) и экспортируйте заново.\n" +
                    "- При экспорте включите 'Apply Modifiers' и используйте FBX Binary (7.4).\n" +
                    "- Для плоских контуров добавьте небольшой Extrude/Solidify (толщину) перед экспортом.\n\n" +
                    "Ошибка при загрузке 3D модели.",
                    "",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                return null;
            }

            var group = new Model3DGroup();

            foreach (var mesh in scene.Meshes)
            {
                var wpfMesh = new MeshGeometry3D();

                for (int vi = 0; vi < mesh.VertexCount; vi++)
                {
                    var v = mesh.Vertices[vi];
                    var p = new Point3D(v.X, v.Y, v.Z);

                    wpfMesh.Positions.Add(p);
                    modelPoints.Add(p);
                }

                if (mesh.FaceCount > 0)
                {
                    foreach (var face in mesh.Faces)
                    {
                        if (face.IndexCount == 3)
                        {
                            wpfMesh.TriangleIndices.Add(face.Indices[0]);
                            wpfMesh.TriangleIndices.Add(face.Indices[1]);
                            wpfMesh.TriangleIndices.Add(face.Indices[2]);
                        }
                        else if (face.IndexCount == 2)
                        {
                            int a = face.Indices[0];
                            int b = face.Indices[1];
                            wpfMesh.TriangleIndices.Add(a);
                            wpfMesh.TriangleIndices.Add(b);
                            wpfMesh.TriangleIndices.Add(a);
                        }
                        else if (face.IndexCount > 3)
                        {
                            int a = face.Indices[0];
                            for (int k = 1; k < face.IndexCount - 1; k++)
                            {
                                wpfMesh.TriangleIndices.Add(a);
                                wpfMesh.TriangleIndices.Add(face.Indices[k]);
                                wpfMesh.TriangleIndices.Add(face.Indices[k + 1]);
                            }
                        }
                    }
                }
                else
                {
                    if (wpfMesh.Positions.Count >= 3)
                    {
                        for (int i = 1; i < wpfMesh.Positions.Count - 1; i++)
                        {
                            wpfMesh.TriangleIndices.Add(0);
                            wpfMesh.TriangleIndices.Add(i);
                            wpfMesh.TriangleIndices.Add(i + 1);
                        }
                    }
                }

                if (wpfMesh.TriangleIndices.Count == 0 || wpfMesh.Positions.Count < 3)
                {
                    continue;
                }

                Color color = Colors.LightGray;
                if (mesh.MaterialIndex >= 0 && mesh.MaterialIndex < scene.MaterialCount)
                {
                    var mat = scene.Materials[mesh.MaterialIndex];
                    var diff = mat.ColorDiffuse;
                    color = Color.FromScRgb(1.0f, diff.R, diff.G, diff.B);
                }

                var brush = new SolidColorBrush(color);
                brush.Freeze();

                var materialGroup = new MaterialGroup();
                materialGroup.Children.Add(new DiffuseMaterial(brush));
                materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)), 20));

                var geom = new GeometryModel3D(wpfMesh, materialGroup)
                {
                    BackMaterial = materialGroup
                };

                group.Children.Add(geom);
            }

            if (group.Children.Count == 0)
            {
                MessageBox.Show(
                    "После обработки мешей не удалось получить пригодную для отображения геометрию.\n" +
                    "Попробуйте в 3D-редакторе конвертировать кривые в mesh и/или добавить небольшую толщину (Extrude/Solidify).",
                    "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                return null;
            }

            return group;
        }

        private void CreatePointsButton_Click(object sender, RoutedEventArgs e)
        {
            if (modelPoints.Count == 0)
            {
                MessageBox.Show("Сначала загрузите FBX-модель, чтобы создать точки.",
                                "Нет данных",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                return;
            }

            modelPointsGroup.Children.Clear();

            for (int i = 0; i < modelPoints.Count; i++)
            {
                var local = modelPoints[i];
                var world = TransformModelPoint(local);

                var color = IsPointInWorkZoneWorld(local) ? Colors.Yellow : Colors.Red;

                var sphere = new Sphere(world, 5.0, color);
                modelPointsGroup.Children.Add(sphere.GetModel3D());
            }

            for (int i = 0; i < modelPoints.Count - 1; i++)
            {
                AddLineToGroup(
                TransformModelPoint(modelPoints[i]),
                TransformModelPoint(modelPoints[i + 1]),
                Colors.Cyan,
                modelPointsGroup
                );
            }
        }

        private void ClearFieldButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var obj in _sceneManager.GetAllObjects().ToList())
            {
                if (obj.IsGridAttached)
                {
                    DetachObjectFromGrid(obj);
                }
                _sceneManager.RemoveObject(obj.Id);
            }

            foreach (var model in loadedModels)
            {
                sceneGroup.Children.Remove(model);
            }
            loadedModels.Clear();

            modelPointsGroup.Children.Clear();
            modelPoints.Clear();

            svgPointsGroup.Children.Clear();
            svgPoints.Clear();

            textPointsGroup.Children.Clear();
            textPoints.Clear();
            originalTextPoints.Clear();

            ClearSheetGrid();
            _gridAttachedObjects.Clear();

            ClearNumberedGrid();
            _gridCells.Clear();
            _objectToCellMap.Clear();
            _cellToObjectMap.Clear();

            UpdateStatus("Все объекты очищены");
        }

        private void ApplyModelOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            if (lastLoadedModelTranslation == null || lastLoadedModelTranslation == null)
            {
                MessageBox.Show("Нет загруженной модели для перемещения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (double.TryParse(ModelOffsetXTextBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tx) &&
                double.TryParse(ModelOffsetYTextBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double ty) &&
                double.TryParse(ModelOffsetZTextBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out double tz))
            {
                lastLoadedModelTranslation.OffsetX = tx;
                lastLoadedModelTranslation.OffsetY = ty;
                lastLoadedModelTranslation.OffsetZ = tz;
            }
            else
            {
                MessageBox.Show("Введите корректные числовые значения для смещения X, Y, Z.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        public class ProgramPoint
        {
            public Point3D Point { get; set; }
            public double R { get; set; }
            public bool IsLift { get; set; } = false;
        }

        private void ExportUPProgrammButton_Click(object sender, RoutedEventArgs e)
        {
            List<ProgramPoint> programPoints = new List<ProgramPoint>();

            foreach (var obj in _sceneManager.GetAllObjects())
            {
                if (!obj.IsVisible) continue;

                double objectR = 0.0;

                if (obj.Type == SceneObjectType.Calligraphy || obj.Type == SceneObjectType.SVG)
                {
                    objectR = CalculateRFromWidthVariation(obj.StrokeWidth);
                }

                if (obj.Type == SceneObjectType.Calligraphy)
                {
                    // ИСПРАВЛЕНИЕ: Добавляем общий список всех точек для этого объекта
                    List<ProgramPoint> objProgramPoints = new List<ProgramPoint>();
                    CalligraphyStroke previousStroke = null;

                    foreach (var stroke in obj.CalligraphyStrokes)
                    {
                        if (stroke.IsLiftMovement)
                        {
                            // Для подъемных движений сохраняем тот же R, но поднимаем кисть
                            double currentR = objectR;

                            if (previousStroke != null && !previousStroke.IsLiftMovement)
                            {
                                // Если предыдущий штрих был рисующим, создаем подъем
                                if (previousStroke.Points.Count > 0)
                                {
                                    // Получаем последнюю точку предыдущего штриха
                                    var lastPoint = previousStroke.Points.Last();
                                    var transformedLastPoint = ApplyObjectTransformation(lastPoint, obj);
                                    var correctedLastPoint = ReverseCoordinateCorrection(transformedLastPoint);

                                    // Добавляем точку с тем же R, но поднятой по Z
                                    objProgramPoints.Add(new ProgramPoint
                                    {
                                        Point = new Point3D(
                                            correctedLastPoint.X,
                                            correctedLastPoint.Y,
                                            correctedLastPoint.Z + 20.0),
                                        R = currentR,
                                        IsLift = true
                                    });
                                }
                            }
                            // ИСПРАВЛЕНИЕ: Не добавляем точки самого lift stroke - они уже включены выше
                        }
                        else
                        {
                            double currentR = objectR;

                            for (int i = 0; i < stroke.Points.Count; i++)
                            {
                                var point = stroke.Points[i];
                                var transformedPoint = ApplyObjectTransformation(point, obj);
                                var correctedPoint = ReverseCoordinateCorrection(transformedPoint);

                                // Для первой точки штриха, если предыдущий был подъемом,
                                // опускаем кисть с сохранением того же R
                                if (i == 0 && previousStroke != null && previousStroke.IsLiftMovement)
                                {
                                    // Добавляем точку опускания
                                    objProgramPoints.Add(new ProgramPoint
                                    {
                                        Point = new Point3D(
                                            correctedPoint.X,
                                            correctedPoint.Y,
                                            correctedPoint.Z + 20.0),
                                        R = currentR,
                                        IsLift = true
                                    });
                                }

                                objProgramPoints.Add(new ProgramPoint
                                {
                                    Point = correctedPoint,
                                    R = currentR,
                                    IsLift = false
                                });
                            }
                        }

                        previousStroke = stroke;
                    }

                    // Добавляем все точки этого объекта в общий список
                    programPoints.AddRange(objProgramPoints);
                }
                else if (obj.Type == SceneObjectType.SVG)
                {
                    // Обработка обычных SVG объектов
                    double currentR = objectR;
                    foreach (var point in obj.Points)
                    {
                        var transformedPoint = ApplyObjectTransformation(point, obj);
                        var correctedPoint = ReverseCoordinateCorrection(transformedPoint);

                        programPoints.Add(new ProgramPoint
                        {
                            Point = correctedPoint,
                            R = currentR,
                            IsLift = false
                        });
                    }
                }
            }


                if (programPoints.Count == 0)
            {
                MessageBox.Show("Нет точек для экспорта!",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Warning);
                return;
            }

            // Получаем настройки из элементов управления XAML
            string progname = ProgramNameTextBox.Text.Trim();
            string progcom = ProgramCommentTextBox.Text.Trim();
            string speed = SpeedTextBox.Text.Trim();
            string motionType = "CNT60"; // значение по умолчанию

            if (MotionTypeComboBox.SelectedItem is ComboBoxItem selectedItem && selectedItem.Tag != null)
            {
                motionType = selectedItem.Tag.ToString();
            }

            // Если имя программы пустое, используем значение по умолчанию
            if (string.IsNullOrWhiteSpace(progname))
                progname = "MODDDEL";

            // Если скорость пустая, используем значение по умолчанию
            if (string.IsNullOrWhiteSpace(speed))
                speed = "250";

            // Ограничиваем длину имени программы (обычно максимум 8 символов для Fanuc)
            if (progname.Length > 8)
                progname = progname.Substring(0, 8);

            // Удаляем недопустимые символы из имени программы
            string cleanProgname = Regex.Replace(progname, @"[^a-zA-Z0-9_]", "");

            // Если после очистки имя стало пустым, используем значение по умолчанию
            if (string.IsNullOrWhiteSpace(cleanProgname))
                cleanProgname = "MODDDEL";

            // Устанавливаем имя файла по умолчанию как имя программы
            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "List files (*.ls)|*.ls|All files (*.*)|*.*",
                FileName = cleanProgname,
                DefaultExt = ".ls"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            try
            {
                using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName, false, System.Text.Encoding.ASCII))
                {
                    int posCount = programPoints.Count;
                    int lineCount = posCount;

                    int progSize = 423 + (61 * posCount);
                    int memorySize = 795 + (57 * posCount);

                    DateTime now = DateTime.Now;
                    string createdDate = now.ToString("dd-MM-yy");
                    string createdTime = now.ToString("HH:mm:ss");

                    // Используем очищенное имя программы в заголовке файла
                    sw.WriteLine($"/PROG  {cleanProgname}");
                    sw.WriteLine("/ATTR");
                    sw.WriteLine($"OWNER\t\t= MNEDITOR;");
                    sw.WriteLine($"COMMENT\t\t= \"{progcom}\";");
                    sw.WriteLine($"PROG_SIZE\t= {progSize};");
                    sw.WriteLine($"CREATE\t\t= DATE {createdDate}  TIME {createdTime};");
                    sw.WriteLine($"MODIFIED\t= DATE {createdDate}  TIME {createdTime};");
                    sw.WriteLine($"FILE_NAME\t= ;");
                    sw.WriteLine($"VERSION\t\t= 0;");
                    sw.WriteLine($"LINE_COUNT\t= {lineCount};");
                    sw.WriteLine($"MEMORY_SIZE\t= {memorySize};");
                    sw.WriteLine($"PROTECT\t\t= READ_WRITE;");
                    sw.WriteLine($"TCD:  STACK_SIZE\t= 0,");
                    sw.WriteLine($"      TASK_PRIORITY\t= 50,");
                    sw.WriteLine($"      TIME_SLICE\t= 0,");
                    sw.WriteLine($"      BUSY_LAMP_OFF\t= 0,");
                    sw.WriteLine($"      ABORT_REQUEST\t= 0,");
                    sw.WriteLine($"      PAUSE_REQUEST\t= 0;");
                    sw.WriteLine($"DEFAULT_GROUP\t= 1,*,*,*,*;");
                    sw.WriteLine($"CONTROL_CODE\t= 00000000 00000000;");
                    sw.WriteLine($"LOCAL_REGISTERS\t= 0,0,0;");
                    sw.WriteLine("/APPL");
                    sw.WriteLine("/APPL");
                    sw.WriteLine("");
                    sw.WriteLine("AUTO_SINGULARITY_HEADER;");
                    sw.WriteLine("  ENABLE_SINGULARITY_AVOIDANCE   : FALSE;");
                    sw.WriteLine("/MN");

                    for (int i = 0; i < programPoints.Count; i++)
                    {
                        sw.WriteLine($"   {i + 1}:L P[{i + 1}] {speed}mm/sec {motionType}    ;");
                    }

                    sw.WriteLine("/POS");

                    for (int i = 0; i < programPoints.Count; i++)
                    {
                        ProgramPoint pp = programPoints[i];
                        Point3D wp = pp.Point;

                        string rFormatted = pp.R.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(8);

                        sw.WriteLine($"P[{i + 1}]{{");
                        sw.WriteLine("   GP1:");
                        sw.WriteLine("\tUF : 9, UT : 9,\t\tCONFIG : 'N U T, 0, 0, 0',");
                        sw.WriteLine($"\tX =   {wp.X.ToString("0.000", CultureInfo.InvariantCulture)}  mm,\tY =   {wp.Y.ToString("0.000", CultureInfo.InvariantCulture)}  mm,\tZ =    {wp.Z.ToString("0.000", CultureInfo.InvariantCulture)}  mm,");
                        sw.WriteLine($"\tW =  -180.000 deg,\tP =       .000 deg,\tR = {rFormatted} deg");
                        sw.WriteLine("};");
                    }

                    sw.WriteLine("/END");
                }

                MessageBox.Show($"Управляющая программа '{cleanProgname}' успешно создана!\nФайл: {IO_Path.GetFileName(saveFileDialog.FileName)}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании программы: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private Point3D ApplyObjectTransformation(Point3D point, SceneObject obj)
        {
            return BuildObjectTransformGroup(obj).Transform(point);
        }

        private T FindVisualChild<T>(DependencyObject parent, string childName = null) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T && (string.IsNullOrEmpty(childName) || ((FrameworkElement)child).Name == childName))
                    return (T)child;

                var result = FindVisualChild<T>(child, childName);
                if (result != null)
                    return result;
            }

            return null;
        }

        private void LoadSvgButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "SVG files (*.svg)|*.svg|All files (*.*)|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                if (!double.TryParse(SvgScaleTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                {
                    scale = 1.0;
                    SvgScaleTextBox.Text = "1.0";
                }

                if (!double.TryParse(SvgOffsetXTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetX))
                {
                    offsetX = 0;
                    SvgOffsetXTextBox.Text = "0";
                }

                if (!double.TryParse(SvgOffsetYTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetY))
                {
                    offsetY = 0;
                    SvgOffsetYTextBox.Text = "0";
                }

                if (!double.TryParse(SvgZTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetZ))
                {
                    offsetZ = 50;
                    SvgZTextBox.Text = "50";
                }

                if (!double.TryParse(SvgRotationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
                {
                    rotation = 0;
                    SvgRotationTextBox.Text = "0";
                }

                SceneObjectType objectType = SceneObjectType.SVG;
                if (EnableCalligraphyCheckBox != null && EnableCalligraphyCheckBox.IsChecked == true)
                {
                    objectType = SceneObjectType.Calligraphy;
                }

                var sceneObject = new SceneObject
                {
                    Type = objectType,
                    Name = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName),
                    FilePath = dialog.FileName,
                    OffsetX = offsetX,
                    OffsetY = offsetY,
                    OffsetZ = offsetZ,
                    Scale = scale,
                    Rotation = rotation,
                    IsGridAttached = _isGridSnapEnabled
                };

                LoadSvgIntoSceneObject(sceneObject);
                DisplaySceneObject(sceneObject);

                if (_isGridSnapEnabled && sceneObject.IsGridAttached)
                {
                    AttachObjectToGrid(sceneObject);
                }

                _sceneManager.AddObject(sceneObject);

                svgPoints.Clear();
                svgPointsGroup.Children.Clear();
                calligraphyStrokes.Clear();

                UpdateStatus($"SVG загружен как объект: {sceneObject.Name}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке SVG: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadSvgIntoSceneObject(SceneObject sceneObject)
        {
            if (string.IsNullOrEmpty(sceneObject.FilePath) || !File.Exists(sceneObject.FilePath))
            {
                MessageBox.Show("Файл SVG не найден.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (sceneObject.IsGridAttached)
            {
                sceneObject.OffsetZ = 0; // Z=0 для объектов на сетке
                sceneObject.Scale = 0.12; // Масштаб 0.12 для объектов на сетке
            }

            try
            {
                double step = double.TryParse(SvgStepTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double s) ? s : 5.0;
                double scale = 1.0; // Загружаем без масштаба
                double offsetX = 0; // Загружаем без смещения
                double offsetY = 0;
                double offsetZ = sceneObject.OffsetZ; // Используем только Z offset

                sceneObject.Points.Clear();
                sceneObject.CalligraphyStrokes.Clear();

                CalligraphyMode mode = CalligraphyMode.SingleStroke;
                if (EnableCalligraphyCheckBox != null && EnableCalligraphyCheckBox.IsChecked == true &&
                    CalligraphyModeComboBox != null && CalligraphyModeComboBox.SelectedItem is ComboBoxItem selectedMode)
                {
                    switch (selectedMode.Tag.ToString())
                    {
                        case "SingleStroke": mode = CalligraphyMode.SingleStroke; break;
                        case "OutlineFill": mode = CalligraphyMode.OutlineFill; break;
                        case "BrushStroke": mode = CalligraphyMode.BrushStroke; break;
                    }
                    sceneObject.CalligraphyMode = mode;
                }

                double liftHeight = 5.0;
                double widthVariation = 0.2;

                if (CalligraphyLiftHeightTextBox != null &&
                    double.TryParse(CalligraphyLiftHeightTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out liftHeight)) { }

                if (StrokeWidthVariationTextBox != null &&
                    double.TryParse(StrokeWidthVariationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out widthVariation))
                {
                    sceneObject.StrokeWidth = widthVariation;
                }

                if (EnableCalligraphyCheckBox != null && EnableCalligraphyCheckBox.IsChecked == true)
                {
                    var strokes = SvgCalligraphyProcessor.LoadSvgAsCalligraphyStrokes(
                        sceneObject.FilePath,
                        stepMm: step,
                        scale: scale, // 1.0
                        offset: new Point3D(offsetX, offsetY, offsetZ), // 0, 0, offsetZ
                        liftHeight: liftHeight,
                        mode: mode,
                        strokeWidthVariation: widthVariation
                    );

                    // ВАЖНОЕ ИСПРАВЛЕНИЕ: Сохраняем исходные точки без трансформаций
                    foreach (var stroke in strokes)
                    {
                        var newStroke = new SvgCalligraphyProcessor.CalligraphyStroke
                        {
                            Points = new List<Point3D>(stroke.Points),
                            IsLiftMovement = stroke.IsLiftMovement,
                            IsNewCharacter = stroke.IsNewCharacter,
                            StrokeWidth = stroke.StrokeWidth
                        };

                        sceneObject.CalligraphyStrokes.Add(newStroke);
                        sceneObject.Points.AddRange(stroke.Points);
                    }
                }
                else
                {
                    var points = SvgToRobotPath.LoadSvgAs3DPoints(
                        sceneObject.FilePath,
                        stepMm: step,
                        scale: scale, // 1.0
                        offset: new Point3D(offsetX, offsetY, offsetZ) // 0, 0, offsetZ
                    );

                    // ВАЖНОЕ ИСПРАВЛЕНИЕ: Сохраняем исходные точки
                    sceneObject.Points.AddRange(points);
                }

                lastSvgFilePath = sceneObject.FilePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке SVG: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplaySceneObject(SceneObject sceneObject)
        {
            var visualGroup = new Model3DGroup();
            ApplyTransformationsToObject(sceneObject);
            visualGroup.Transform = sceneObject.TransformGroup;

            switch (sceneObject.Type)
            {
                case SceneObjectType.SVG:
                    DisplaySvgObject(sceneObject, visualGroup);
                    break;
                case SceneObjectType.Text:
                    DisplayTextObject(sceneObject, visualGroup);
                    break;
                case SceneObjectType.Calligraphy:
                    DisplayCalligraphyObject(sceneObject, visualGroup);
                    break;
                case SceneObjectType.Model3D:
                    DisplayModel3DObject(sceneObject, visualGroup);
                    break;
            }

            sceneObject.VisualModel = visualGroup;
            sceneGroup.Children.Add(visualGroup);
        }

        private void DisplaySvgObject(SceneObject obj, Model3DGroup visualGroup)
        {
            foreach (var point in obj.Points)
            {
                var sphere = new Sphere(point, 3.0, Colors.Orange);
                visualGroup.Children.Add(sphere.GetModel3D());
            }

            for (int i = 0; i < obj.Points.Count - 1; i++)
            {
                AddLineToGroup(obj.Points[i], obj.Points[i + 1], Colors.Cyan, visualGroup);
            }
        }

        private void DisplayCalligraphyObject(SceneObject obj, Model3DGroup visualGroup)
        {
            foreach (var stroke in obj.CalligraphyStrokes)
            {
                if (stroke.Points.Count == 0)
                    continue;

                foreach (var point in stroke.Points)
                {
                    Color pointColor = stroke.IsLiftMovement ? Colors.Red :
                                      (stroke.IsNewCharacter ? Colors.Green : Colors.Blue);
                    var sphere = new Sphere(point, stroke.IsLiftMovement ? 2.0 : 3.0, pointColor);
                    visualGroup.Children.Add(sphere.GetModel3D());
                }

                if (!stroke.IsLiftMovement && stroke.Points.Count > 1)
                {
                    for (int i = 0; i < stroke.Points.Count - 1; i++)
                    {
                        var point1 = stroke.Points[i];
                        var point2 = stroke.Points[i + 1];
                        double lineThickness = stroke.StrokeWidth;
                        AddLineToGroup(point1, point2, Colors.Cyan, visualGroup, lineThickness);
                    }
                }
            }
        }

        private void DisplayTextObject(SceneObject obj, Model3DGroup visualGroup)
        {
            foreach (var point in obj.Points)
            {
                var sphere = new Sphere(point, 2.0, Colors.Magenta);
                visualGroup.Children.Add(sphere.GetModel3D());
            }

            for (int i = 0; i < obj.Points.Count - 1; i++)
            {
                Color lineColor;
                if (Math.Abs(obj.Points[i].Z - obj.Points[i + 1].Z) > 5)
                {
                    lineColor = Colors.Red;
                }
                else
                {
                    lineColor = Colors.Purple;
                }

                AddLineToGroup(obj.Points[i], obj.Points[i + 1], lineColor, visualGroup);
            }
        }

        private void DisplayModel3DObject(SceneObject obj, Model3DGroup visualGroup)
        {
            if (obj.VisualModel != null)
            {
                visualGroup.Children.Add(obj.VisualModel);
            }
            else
            {
                var cube = CreateSimpleCube(new Point3D(0, 0, 0), 10, Colors.Gray);
                visualGroup.Children.Add(cube);
            }
        }

        private Model3D CreateSimpleCube(Point3D center, double size, Color color)
        {
            var mesh = new MeshGeometry3D();

            var halfSize = size / 2;
            mesh.Positions.Add(new Point3D(center.X - halfSize, center.Y - halfSize, center.Z - halfSize));
            mesh.Positions.Add(new Point3D(center.X + halfSize, center.Y - halfSize, center.Z - halfSize));
            mesh.Positions.Add(new Point3D(center.X + halfSize, center.Y + halfSize, center.Z - halfSize));
            mesh.Positions.Add(new Point3D(center.X - halfSize, center.Y + halfSize, center.Z - halfSize));
            mesh.Positions.Add(new Point3D(center.X - halfSize, center.Y - halfSize, center.Z + halfSize));
            mesh.Positions.Add(new Point3D(center.X + halfSize, center.Y - halfSize, center.Z + halfSize));
            mesh.Positions.Add(new Point3D(center.X + halfSize, center.Y + halfSize, center.Z + halfSize));
            mesh.Positions.Add(new Point3D(center.X - halfSize, center.Y + halfSize, center.Z + halfSize));

            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(4);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(2);

            var material = new DiffuseMaterial(new SolidColorBrush(color));
            var geometryModel = new GeometryModel3D(mesh, material);

            return geometryModel;
        }

        private void UpdateObjectVisual(SceneObject obj)
        {
            if (obj.VisualModel != null)
            {
                sceneGroup.Children.Remove(obj.VisualModel);
                obj.VisualModel = null;
            }

            DisplaySceneObject(obj);
        }



        private Point3D RotatePoint(Point3D point, double angleDegrees, Point3D center = default)
        {
            double angleRad = angleDegrees * Math.PI / 180.0;
            double cosAngle = Math.Cos(angleRad);
            double sinAngle = Math.Sin(angleRad);

            double x = point.X - center.X;
            double y = point.Y - center.Y;

            double newX = x * cosAngle - y * sinAngle;
            double newY = x * sinAngle + y * cosAngle;

            return new Point3D(newX + center.X, newY + center.Y, point.Z);
        }

        private List<Point3D> RotatePoints(List<Point3D> points, double angleDegrees)
        {
            if (Math.Abs(angleDegrees) < 0.001)
                return new List<Point3D>(points);

            double centerX = 0, centerY = 0;
            foreach (var point in points)
            {
                centerX += point.X;
                centerY += point.Y;
            }
            if (points.Count > 0)
            {
                centerX /= points.Count;
                centerY /= points.Count;
            }

            Point3D center = new Point3D(centerX, centerY, 0);
            List<Point3D> rotatedPoints = new List<Point3D>();

            foreach (var point in points)
            {
                rotatedPoints.Add(RotatePoint(point, angleDegrees, center));
            }

            return rotatedPoints;
        }

        private Point3D ApplyCoordinateCorrection(Point3D point)
        {
            if (!APPLY_COORDINATE_CORRECTION)
                return point;

            // Поворачиваем точку на -90° вокруг оси Z
            double rad = COORDINATE_CORRECTION_ANGLE * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double newX = point.X * cos - point.Y * sin;
            double newY = point.X * sin + point.Y * cos;

            return new Point3D(newX, newY, point.Z);
        }

        private Point3D ReverseCoordinateCorrection(Point3D point)
        {
            if (!APPLY_COORDINATE_CORRECTION)
                return point;

            // Обратный поворот (+90°) вокруг оси Z
            double rad = -COORDINATE_CORRECTION_ANGLE * Math.PI / 180.0;
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);

            double newX = point.X * cos - point.Y * sin;
            double newY = point.X * sin + point.Y * cos;

            return new Point3D(newX, newY, point.Z);
        }


        private void LoadSvgWithCurrentParameters()
        {
            if (string.IsNullOrEmpty(lastSvgFilePath) || !File.Exists(lastSvgFilePath))
            {
                MessageBox.Show("Сначала выберите SVG файл.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                if (!double.TryParse(SvgScaleTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                {
                    scale = 1.0;
                    SvgScaleTextBox.Text = "1.0";
                }

                if (!double.TryParse(SvgStepTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
                {
                    step = 5.0;
                    SvgStepTextBox.Text = "5.0";
                }

                if (!double.TryParse(SvgZTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double z))
                {
                    z = 50.0;
                    SvgZTextBox.Text = "50.0";
                }

                if (!double.TryParse(SvgOffsetXTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetX))
                {
                    offsetX = 0;
                    SvgOffsetXTextBox.Text = "0";
                }

                if (!double.TryParse(SvgOffsetYTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetY))
                {
                    offsetY = 0;
                    SvgOffsetYTextBox.Text = "0";
                }

                if (!double.TryParse(SvgRotationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rotationAngle))
                {
                    rotationAngle = 0;
                    SvgRotationTextBox.Text = "0";
                }

                svgRotationAngle = rotationAngle;

                if (StrokeWidthVariationTextBox != null &&
                    !double.TryParse(StrokeWidthVariationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out strokeWidthVariationForR))
                {
                    strokeWidthVariationForR = 0.0;
                }

                if (strokeWidthVariationForR > 2.0)
                {
                    strokeWidthVariationForR = 2.0;
                    StrokeWidthVariationTextBox.Text = "2.0";
                }

                svgPoints.Clear();
                svgPointsGroup.Children.Clear();
                calligraphyStrokes.Clear();

                CalligraphyMode mode = CalligraphyMode.SingleStroke;
                if (EnableCalligraphyCheckBox != null && EnableCalligraphyCheckBox.IsChecked == true &&
                    CalligraphyModeComboBox != null && CalligraphyModeComboBox.SelectedItem is ComboBoxItem selectedMode)
                {
                    switch (selectedMode.Tag.ToString())
                    {
                        case "SingleStroke": mode = CalligraphyMode.SingleStroke; break;
                        case "OutlineFill": mode = CalligraphyMode.OutlineFill; break;
                        case "BrushStroke": mode = CalligraphyMode.BrushStroke; break;
                    }
                }

                double liftHeight = 5.0;
                double widthVariation = 0.2;

                if (CalligraphyLiftHeightTextBox != null &&
                    !double.TryParse(CalligraphyLiftHeightTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out liftHeight))
                {
                    liftHeight = 5.0;
                }

                if (StrokeWidthVariationTextBox != null &&
                    !double.TryParse(StrokeWidthVariationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out widthVariation))
                {
                    widthVariation = 0.2;
                }

                if (EnableCalligraphyCheckBox != null && EnableCalligraphyCheckBox.IsChecked == true)
                {
                    calligraphyStrokes = SvgCalligraphyProcessor.LoadSvgAsCalligraphyStrokes(
                        lastSvgFilePath,
                        stepMm: step,
                        scale: scale,
                        offset: new Point3D(offsetX, offsetY, z),
                        liftHeight: liftHeight,
                        mode: mode,
                        strokeWidthVariation: widthVariation
                    );

                    foreach (var stroke in calligraphyStrokes)
                    {
                        if (stroke.Points.Count == 0)
                            continue;

                        var rotatedPoints = RotatePoints(stroke.Points, svgRotationAngle);

                        foreach (var point in rotatedPoints)
                        {
                            var correctedPoint = ApplyCoordinateCorrection(point);
                            svgPoints.Add(point);

                            Color pointColor = stroke.IsLiftMovement ? Colors.Red :
                                              (stroke.IsNewCharacter ? Colors.Green : Colors.Blue);
                            var sphere = new Sphere(correctedPoint, stroke.IsLiftMovement ? 2.0 : 3.0, pointColor);
                            svgPointsGroup.Children.Add(sphere.GetModel3D());
                        }

                        if (!stroke.IsLiftMovement && rotatedPoints.Count > 1)
                        {
                            for (int i = 0; i < rotatedPoints.Count - 1; i++)
                            {
                                var correctedPoint1 = ApplyCoordinateCorrection(rotatedPoints[i]);
                                var correctedPoint2 = ApplyCoordinateCorrection(rotatedPoints[i + 1]);

                                double lineThickness = stroke.StrokeWidth;
                                AddLineToGroup(correctedPoint1, correctedPoint2, Colors.Cyan,
                                              svgPointsGroup, lineThickness);
                            }
                        }
                    }

                    UpdateStatus($"SVG загружен в каллиграфическом режиме: {calligraphyStrokes.Count} штрихов");
                }
                else
                {
                    var points = SvgToRobotPath.LoadSvgAs3DPoints(
                        lastSvgFilePath,
                        stepMm: step,
                        scale: scale,
                        offset: new Point3D(offsetX, offsetY, z)
                    );

                    points = RotatePoints(points, svgRotationAngle);

                    List<Point3D> correctedPoints = new List<Point3D>();
                    foreach (var point in points)
                    {
                        var correctedPoint = ApplyCoordinateCorrection(point);
                        correctedPoints.Add(correctedPoint);
                        svgPoints.Add(point);

                        var sphere = new Sphere(correctedPoint, 3.0, Colors.Orange);
                        svgPointsGroup.Children.Add(sphere.GetModel3D());
                    }

                    for (int i = 0; i < correctedPoints.Count - 1; i++)
                    {
                        AddLineToGroup(
                            correctedPoints[i],
                            correctedPoints[i + 1],
                            Colors.Cyan,
                            svgPointsGroup);
                    }

                    UpdateStatus($"SVG загружен: {svgPoints.Count} точек, файл: {IO_Path.GetFileName(lastSvgFilePath)}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке SVG: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private double CalculateRFromWidthVariation(double widthVariation)
        {
            // Исправляем зависимость R
            if (widthVariation <= 0.1)
                return 0.0;   // R = 0° (вертикально вниз)

            if (widthVariation >= 2.0)
                return 90.0;  // R = 90° (горизонтально вправо)

            // Линейная интерполяция: 0.1 -> 0°, 2.0 -> 90°
            double normalized = (widthVariation - 0.1) / (2.0 - 0.1);
            return normalized * 90.0;
        }

        private void ApplySvgParametersButton_Click(object sender, RoutedEventArgs e)
        {
            if (_sceneManager.SelectedObject != null &&
                (_sceneManager.SelectedObject.Type == SceneObjectType.SVG ||
                 _sceneManager.SelectedObject.Type == SceneObjectType.Calligraphy))
            {
                var obj = _sceneManager.SelectedObject;

                if (double.TryParse(SvgScaleTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                    obj.Scale = scale;

                if (double.TryParse(SvgOffsetXTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetX))
                    obj.OffsetX = offsetX;

                if (double.TryParse(SvgOffsetYTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetY))
                    obj.OffsetY = offsetY;

                if (double.TryParse(SvgZTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetZ))
                    obj.OffsetZ = offsetZ;

                if (double.TryParse(SvgRotationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
                    obj.Rotation = rotation;

                if (obj.IsGridAttached && _isGridSnapEnabled)
                {
                    SnapObjectToGrid(obj);
                }

                LoadSvgIntoSceneObject(obj);
                UpdateObjectVisual(obj);

                UpdateStatus($"Параметры SVG обновлены для объекта: {obj.Name}");
            }
            else
            {
                LoadSvgWithCurrentParameters();
            }
        }

        private void UpdateStatus(string message)
        {
            Console.WriteLine($"Status: {message}");
        }

        public void LoadTextPoints(List<Point3D> points, string fontFamily, float fontSize,
     float lineSpacing, float depth, float scale, float liftHeight,
     string originalText, float pointStep,
     TextGenerationMode textMode = TextGenerationMode.Contour,
     bool separateCharacters = true)
        {
            try
            {
                var sceneObject = new SceneObject
                {
                    Type = SceneObjectType.Text,
                    Name = $"Текст: {originalText.Substring(0, Math.Min(20, originalText.Length))}...",
                    TextContent = originalText,
                    FontFamily = fontFamily,
                    FontSize = fontSize,
                    OffsetZ = 0, // Z=0 для текста по умолчанию
                    Scale = 0.12, // Масштаб 0.12 по умолчанию для текста
                    Points = new List<Point3D>(points),
                    IsGridAttached = false // Сначала не привязан
                };

                // Если есть активная сетка с номерами, привязываем к свободной ячейке
                DisplaySceneObject(sceneObject);
                _sceneManager.AddObject(sceneObject);

                if (_isNumberedGridVisible && _gridCells.Count > 0)
                {
                    var freeCell = _gridCells.FirstOrDefault(c => !c.HasObject);
                    if (freeCell != null)
                    {
                        sceneObject.IsGridAttached = true;
                        MoveObjectToCell(sceneObject, freeCell);
                    }
                }

                this.textFontFamily = fontFamily;
                this.textFontSize = fontSize;
                this.textLineSpacing = lineSpacing;
                this.textDepth = depth;
                this.textScale = scale;
                this.textLiftHeight = liftHeight;
                this.originalText = originalText;
                this.textPointStep = pointStep;
                this.textGenerationMode = textMode;
                this.textSeparateCharacters = separateCharacters;

                // Обновляем поля ввода
                if (TextOffsetXTextBox != null)
                    TextOffsetXTextBox.Text = sceneObject.OffsetX.ToString(CultureInfo.InvariantCulture);
                if (TextOffsetYTextBox != null)
                    TextOffsetYTextBox.Text = sceneObject.OffsetY.ToString(CultureInfo.InvariantCulture);
                if (TextOffsetZTextBox != null)
                    TextOffsetZTextBox.Text = sceneObject.OffsetZ.ToString(CultureInfo.InvariantCulture);
                if (TextScaleTextBox != null)
                    TextScaleTextBox.Text = sceneObject.Scale.ToString(CultureInfo.InvariantCulture);
                if (TextRotationTextBox != null)
                    TextRotationTextBox.Text = "0";
                if (TextPointStepTextBox != null)
                    TextPointStepTextBox.Text = pointStep.ToString("F2", CultureInfo.InvariantCulture);
                if (TextSeparateCharactersCheckBox != null)
                    TextSeparateCharactersCheckBox.IsChecked = separateCharacters;

                textPoints.Clear();
                textPointsGroup.Children.Clear();
                originalTextPoints.Clear();

                UpdateStatus($"Текст загружен как объект: {sceneObject.Name}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке текстовых точек: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyAndRegenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (double.TryParse(TextOffsetXTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetX))
                    textOffsetX = offsetX;
                else
                    textOffsetX = 0;

                if (double.TryParse(TextOffsetYTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetY))
                    textOffsetY = offsetY;
                else
                    textOffsetY = 0;

                if (double.TryParse(TextOffsetZTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double offsetZ))
                    textOffsetZ = offsetZ;
                else
                    textOffsetZ = 50;

                if (double.TryParse(TextScaleTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double scale))
                    textScale = scale;
                else
                    textScale = 1.0;

                if (double.TryParse(TextRotationTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double rotation))
                    textRotation = rotation;
                else
                    textRotation = 0;

                if (!float.TryParse(TextPointStepTextBox.Text.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out float pointStep))
                {
                    pointStep = 0.5f;
                }
                textPointStep = pointStep;
                textInvertX = TextInvertXCheckBox.IsChecked == true;
                textInvertY = TextInvertYCheckBox.IsChecked == true;
                textSeparateCharacters = TextSeparateCharactersCheckBox.IsChecked == true;

                if (TextModeComboBox.SelectedItem is ComboBoxItem selectedItem &&
                    selectedItem.Tag is TextGenerationMode mode)
                {
                    textGenerationMode = mode;
                }

                if (!string.IsNullOrEmpty(originalText))
                {
                    RegenerateTextWithCurrentParameters();
                }
                else if (originalTextPoints.Count > 0)
                {
                    ApplyTextTransformations();
                    UpdateStatus($"Применены параметры трансформации (X={textOffsetX}, Y={textOffsetY}, " +
                                $"Z={textOffsetZ}, Scale={textScale}, Rotation={textRotation})");
                }
                else
                {
                    MessageBox.Show("Нет текстовых точек для обработки.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обработке текста: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyTextTransformations()
        {
            textPoints.Clear();
            textPointsGroup.Children.Clear();

            if (originalTextPoints.Count == 0)
                return;

            foreach (var originalPoint in originalTextPoints)
            {
                double scaledX = originalPoint.X * textScale;
                double scaledY = originalPoint.Y * textScale;
                double scaledZ = originalPoint.Z;

                double rad = textRotation * Math.PI / 180.0;
                double cos = Math.Cos(rad);
                double sin = Math.Sin(rad);

                double dx = scaledX;
                double dy = scaledY;

                double rotatedX = dx * cos - dy * sin;
                double rotatedY = dx * sin + dy * cos;

                double transformedX = rotatedX + textOffsetX;
                double transformedY = rotatedY + textOffsetY;
                double transformedZ = scaledZ + textOffsetZ;

                var transformedPoint = new Point3D(transformedX, transformedY, transformedZ);
                textPoints.Add(transformedPoint);

                var correctedPoint = ApplyCoordinateCorrection(transformedPoint);

                var sphere = new Sphere(correctedPoint, 2.0, Colors.Magenta);
                textPointsGroup.Children.Add(sphere.GetModel3D());
            }

            for (int i = 0; i < textPoints.Count - 1; i++)
            {
                var correctedPoint1 = ApplyCoordinateCorrection(textPoints[i]);
                var correctedPoint2 = ApplyCoordinateCorrection(textPoints[i + 1]);

                Color lineColor;
                if (Math.Abs(textPoints[i].Z - textPoints[i + 1].Z) > 5)
                {
                    lineColor = Colors.Red;
                }
                else
                {
                    lineColor = Colors.Purple;
                }

                AddLineToGroup(correctedPoint1, correctedPoint2, lineColor, textPointsGroup);
            }
        }

        private void ApplySheetTransformButton_Click(object sender, RoutedEventArgs e)
        {
            if (SheetOffsetXTextBox == null || SheetOffsetYTextBox == null ||
                SheetRotationTextBox == null || SheetScaleTextBox == null)
                return;

            ApplySheetTransform(
                SheetOffsetXTextBox.Text,
                SheetOffsetYTextBox.Text,
                "0", // Z смещение всегда 0 для листа
                SheetRotationTextBox.Text,
                SheetScaleTextBox.Text
            );
        }

        private void RegenerateTextWithCurrentParameters()
        {
            try
            {
                List<Point3D> points = TextToPointsGenerator.CreateTextPoints(
                    text: originalText,
                    mode: textGenerationMode,
                    fontName: textFontFamily,
                    fontSize: textFontSize,
                    depth: textDepth,
                    scale: 1.0f, // ВАЖНО: масштаб будет применен через TransformGroup
                    offset: new Point3D(0, 0, 0),
                    liftHeight: textLiftHeight,
                    pointStep: textPointStep,
                    separateCharacters: textSeparateCharacters);

                if (textInvertX || textInvertY)
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        double x = points[i].X;
                        double y = points[i].Y;

                        if (textInvertX) x = -x;
                        if (textInvertY) y = -y;

                        points[i] = new Point3D(x, y, points[i].Z);
                    }
                }

                originalTextPoints.Clear();
                originalTextPoints.AddRange(points);

                ApplyTextTransformations();

                UpdateStatus($"Текст перегенерирован: режим={textGenerationMode}, " +
                            $"шаг={textPointStep}, инверсия X={textInvertX}, инверсия Y={textInvertY}, " +
                            $"трансформации: X={textOffsetX}, Y={textOffsetY}, Z={textOffsetZ}, " +
                            $"Scale={textScale}, Rotation={textRotation}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при перегенерации текста: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearTextPointsButton_Click(object sender, RoutedEventArgs e)
        {
            textPoints.Clear();
            originalTextPoints.Clear();
            originalText = string.Empty;
            textPointsGroup.Children.Clear();

            textOffsetX = 0;
            textOffsetY = 0;
            textOffsetZ = 50;
            textScale = 1.0;
            textRotation = 0;
            textPointStep = 0.5f;
            textInvertX = false;
            textInvertY = false;
            textSeparateCharacters = true;

            TextOffsetXTextBox.Text = "0";
            TextOffsetYTextBox.Text = "0";
            TextOffsetZTextBox.Text = "50";
            TextScaleTextBox.Text = "1.0";
            TextRotationTextBox.Text = "0";
            TextPointStepTextBox.Text = "0.5";
            TextInvertXCheckBox.IsChecked = false;
            TextInvertYCheckBox.IsChecked = false;
            TextSeparateCharactersCheckBox.IsChecked = true;

            UpdateStatus("Текстовые точки очищены");
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex(@"^[0-9]*(?:\.[0-9]*)?$");
            TextBox textBox = sender as TextBox;
            string proposedText = textBox.Text.Insert(textBox.SelectionStart, e.Text);

            if (!regex.IsMatch(proposedText))
            {
                e.Handled = true;
                return;
            }

            bool isStrokeWidth = textBox == _objectStrokeWidthTextBox;
            bool isLiftHeight = textBox.Name == "liftHeightTextBox" ||
                               (textBox.Tag != null && textBox.Tag.ToString() == "liftHeight");

            if (isStrokeWidth)
            {
                if (double.TryParse(proposedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    if (value > 2.0)
                    {
                        textBox.Text = "2.0";
                        textBox.CaretIndex = textBox.Text.Length;
                        e.Handled = true;
                    }
                }
            }
            else if (isLiftHeight)
            {
                if (double.TryParse(proposedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    if (value > 100.0)
                    {
                        textBox.Text = "100.0";
                        textBox.CaretIndex = textBox.Text.Length;
                        e.Handled = true;
                    }
                    else if (value < 5.0)
                    {
                        textBox.Text = "5.0";
                        textBox.CaretIndex = textBox.Text.Length;
                        e.Handled = true;
                    }
                }
            }
            else
            {
                if (double.TryParse(proposedText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    if (value > 2.0)
                    {
                        textBox.Text = "2.0";
                        textBox.CaretIndex = textBox.Text.Length;
                        e.Handled = true;
                    }
                }
            }
        }

        private void ResetSheetTransformButton_Click(object sender, RoutedEventArgs e)
        {
            ResetSheetTransform();

            // Сбрасываем значения в TextBox'ах
            if (SheetOffsetXTextBox != null) SheetOffsetXTextBox.Text = "0";
            if (SheetOffsetYTextBox != null) SheetOffsetYTextBox.Text = "0";

            if (SheetRotationTextBox != null) SheetRotationTextBox.Text = "0";
            if (SheetScaleTextBox != null) SheetScaleTextBox.Text = "1.0";
        }



        private void AutoAttachAllObjectsToGrid()
        {
            if (!_isNumberedGridVisible)
            {
                MessageBox.Show("Сначала включите сетку с номерами.", "Информация",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int cellNumber = 1;

            foreach (var obj in _sceneManager.GetAllObjects())
            {
                if (obj.IsGridAttached) continue;

                // Находим свободную ячейку
                var cell = FindCellByNumber(cellNumber);
                while (cell != null && cell.HasObject && cellNumber <= 70)
                {
                    cellNumber++;
                    cell = FindCellByNumber(cellNumber);
                }

                if (cell == null) break;

                // Привязываем объект к ячейке
                MoveObjectToCell(obj, cell);
                cellNumber++;
            }

            UpdateStatus("Все объекты автоматически привязаны к сетке");
        }

        private void AutoAttachAllButton_Click(object sender, RoutedEventArgs e)
        {
            AutoAttachAllObjectsToGrid();
        }

        private void ToggleGridInversionCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _invertGrid = true;

            // Обновляем отображение сетки если она видима
            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
            }

            if (_isNumberedGridVisible)
            {
                CreateNumberedGrid();
            }

            // Обновляем все объекты, привязанные к сетке
            UpdateGridAttachedObjects();

            UpdateStatus("Инверсия сетки включена");
        }

        private void ToggleGridInversionCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _invertGrid = false;

            // Обновляем отображение сетки если она видима
            if (_isSheetGridVisible)
            {
                DrawSheetGrid();
            }

            if (_isNumberedGridVisible)
            {
                CreateNumberedGrid();
            }

            // Обновляем все объекты, привязанные к сетке
            UpdateGridAttachedObjects();

            UpdateStatus("Инверсия сетки выключена");
        }
    }
}