using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Input;
using Microsoft.Win32;
using System.Globalization;

namespace FanucProgrammer
{
    /// <summary>
    /// Interaction logic for SimulateWindow.xaml
    /// </summary>
    public partial class SimulateWindow : Window
    {
        private List<Point3D> points = new List<Point3D>();
        private readonly List<Model3D> loadedModels = new List<Model3D>();
        private Model3D lastLoadedModel;
        private TranslateTransform3D lastLoadedModelTranslation = new TranslateTransform3D();
        private Model3DGroup sceneGroup;
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

        public SimulateWindow()
        {
            InitializeComponent();
            sceneGroup = (Model3DGroup)((ModelVisual3D)Viewport3D.Children[0]).Content;
            InitializeScene();
            // Capture keyboard input at the window level
            this.PreviewKeyDown += SimulateWindow_KeyDown;
            this.Focusable = true;
            this.Loaded += (s, e) =>
            {
                // Ensure keyboard focus so WASD works immediately
                this.Focus();
                Keyboard.Focus(this);
                UpdateCamera();
            };
        }

        private void InitializeScene()
        {
            // Check if lighting already exists
            bool hasLighting = false;
            foreach (Model3D child in sceneGroup.Children)
            {
                if (child is AmbientLight || child is DirectionalLight)
                {
                    hasLighting = true;
                    break;
                }
            }
            
            // Only add base elements if they don't exist
            if (!hasLighting)
            {
                // Lighting is already in XAML, so we don't need to add it here
            }
            
            // Draw axes
            DrawAxes();
            
            // Draw zero plane
            DrawZeroPlane();
            
            // Draw work zone
            DrawWorkZone();
        }

        private void DrawAxes()
        {
            // X-axis (Red)
            AddLine(new Point3D(0, 0, 0), new Point3D(100, 0, 0), Colors.Red);
            // Y-axis (Green)
            AddLine(new Point3D(0, 0, 0), new Point3D(0, 100, 0), Colors.Green);
            // Z-axis (Blue)
            AddLine(new Point3D(0, 0, 0), new Point3D(0, 0, 100), Colors.Blue);
        }

        private void DrawZeroPlane()
        {
            float gridSize = 200.0f;
            float gridStep = 50.0f;

            // Draw semi-transparent plane
            MeshGeometry3D planeMesh = new MeshGeometry3D();
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
            Color planeColor = Color.FromArgb(80, 100, 100, 150); // Semi-transparent blue-gray
            planeGeometry.Material = new DiffuseMaterial(new SolidColorBrush(planeColor));
            planeGeometry.BackMaterial = new DiffuseMaterial(new SolidColorBrush(planeColor));

            Model3DGroup planeGroup = new Model3DGroup();
            planeGroup.Children.Add(planeGeometry);
            sceneGroup.Children.Add(planeGroup);

            // Draw grid lines
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

        private void DrawWorkZone()
        {
            // Draw work zone with semi-transparent faces
            Color zoneFaceColor = Color.FromArgb(25, 0, 255, 0); // More transparent green
            Color zoneLineColor = Color.FromArgb(200, 0, 200, 0); // Darker green for lines

            // Create faces of the work zone box
            // Front face (Z = MAX_Z)
           

            // Draw wireframe edges
            // Bottom face edges
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), zoneLineColor);

            // Top face edges
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), zoneLineColor);

            // Vertical edges
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), zoneLineColor);
            AddLine(new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z), new Point3D(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z), zoneLineColor);
        }

        private void AddQuadFace(Point3D p1, Point3D p2, Point3D p3, Point3D p4, Color color)
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            mesh.Positions.Add(p1);
            mesh.Positions.Add(p2);
            mesh.Positions.Add(p3);
            mesh.Positions.Add(p4);

            // First triangle
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(1);
            mesh.TriangleIndices.Add(2);
            // Second triangle
            mesh.TriangleIndices.Add(0);
            mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(3);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Geometry = mesh;
            var transparentBrush = new SolidColorBrush(color);
            transparentBrush.Freeze();
            MaterialGroup materialGroup = new MaterialGroup();
            materialGroup.Children.Add(new DiffuseMaterial(transparentBrush));
            materialGroup.Children.Add(new SpecularMaterial(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), 30));

            geometry.Material = materialGroup;
            geometry.BackMaterial = materialGroup;

            Model3DGroup faceGroup = new Model3DGroup();
            faceGroup.Children.Add(geometry);
            sceneGroup.Children.Add(faceGroup);
        }

        private void AddLine(Point3D start, Point3D end, Color color)
        {
            // Create a thin cylinder for the line
            Vector3D direction = end - start;
            double length = direction.Length;
            direction.Normalize();

            // Create a simple line using a thin box
            MeshGeometry3D mesh = new MeshGeometry3D();
            
            // Create a thin rectangular prism along the line
            Vector3D up = new Vector3D(0, 0, 1);
            if (Math.Abs(Vector3D.DotProduct(direction, up)) > 0.9)
                up = new Vector3D(1, 0, 0);
            
            Vector3D right = Vector3D.CrossProduct(direction, up);
            right.Normalize();
            Vector3D actualUp = Vector3D.CrossProduct(right, direction);
            actualUp.Normalize();

            double thickness = 0.5;
            Point3D p1 = start + right * thickness + actualUp * thickness;
            Point3D p2 = start + right * thickness - actualUp * thickness;
            Point3D p3 = start - right * thickness - actualUp * thickness;
            Point3D p4 = start - right * thickness + actualUp * thickness;
            Point3D p5 = end + right * thickness + actualUp * thickness;
            Point3D p6 = end + right * thickness - actualUp * thickness;
            Point3D p7 = end - right * thickness - actualUp * thickness;
            Point3D p8 = end - right * thickness + actualUp * thickness;

            // Add vertices
            mesh.Positions.Add(p1); mesh.Positions.Add(p2); mesh.Positions.Add(p3); mesh.Positions.Add(p4);
            mesh.Positions.Add(p5); mesh.Positions.Add(p6); mesh.Positions.Add(p7); mesh.Positions.Add(p8);

            // Add triangles for the box
            // Front face
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(2);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(3);
            // Back face
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(6);
            // Top face
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(4); mesh.TriangleIndices.Add(5);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(1);
            // Bottom face
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(2); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(7);
            // Right face
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(5); mesh.TriangleIndices.Add(6);
            mesh.TriangleIndices.Add(1); mesh.TriangleIndices.Add(6); mesh.TriangleIndices.Add(2);
            // Left face
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(3); mesh.TriangleIndices.Add(7);
            mesh.TriangleIndices.Add(0); mesh.TriangleIndices.Add(7); mesh.TriangleIndices.Add(4);

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Geometry = mesh;
            geometry.Material = new DiffuseMaterial(new SolidColorBrush(color));

            Model3DGroup lineGroup = new Model3DGroup();
            lineGroup.Children.Add(geometry);

            sceneGroup.Children.Add(lineGroup);
        }

        private Model3DGroup LoadObjModel(string filePath)
        {
            var positions = new List<Point3D>();
            var triangleIndices = new List<int>();

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.StartsWith("v "))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        double y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                        double z = double.Parse(parts[3], CultureInfo.InvariantCulture);
                        positions.Add(new Point3D(x, y, z));
                    }
                }
                else if (trimmed.StartsWith("f "))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 4)
                        continue;

                    // поддерживаем треугольники и простую триангуляцию многоугольников (фан-метод)
                    var vertexIndices = new List<int>();
                    for (int i = 1; i < parts.Length; i++)
                    {
                        var token = parts[i];
                        var indexPart = token.Split('/')[0];
                        if (int.TryParse(indexPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
                        {
                            // OBJ индексы 1-бейзовые
                            vertexIndices.Add(idx - 1);
                        }
                    }

                    if (vertexIndices.Count < 3)
                        continue;

                    for (int i = 1; i < vertexIndices.Count - 1; i++)
                    {
                        triangleIndices.Add(vertexIndices[0]);
                        triangleIndices.Add(vertexIndices[i]);
                        triangleIndices.Add(vertexIndices[i + 1]);
                    }
                }
            }

            if (positions.Count == 0 || triangleIndices.Count == 0)
                throw new InvalidDataException("Файл OBJ не содержит геометрии (v/f).");

            var mesh = new MeshGeometry3D();
            foreach (var p in positions)
            {
                mesh.Positions.Add(p);
            }
            foreach (var idx in triangleIndices)
            {
                mesh.TriangleIndices.Add(idx);
            }

            var material = new DiffuseMaterial(new SolidColorBrush(Colors.LightGray));
            var geometry = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };

            var group = new Model3DGroup();
            group.Children.Add(geometry);
            return group;
        }

        private void AddPoint(Point3D point, int index)
        {
            bool inWorkZone = IsPointInWorkZone(point);
            Color pointColor = inWorkZone ? Colors.Yellow : Colors.Red;

            // Create a sphere for the point with larger size
            Sphere sphere = new Sphere(point, 8.0, pointColor);
            sceneGroup.Children.Add(sphere.GetModel3D());

            // Add label with number
            AddPointLabel(point, index);
        }

        private void AddPointLabel(Point3D point, int index)
        {
            // Create a small text indicator using 3D geometry
            // Position label slightly above and to the side of the point
            Point3D labelBase = new Point3D(point.X + 15, point.Y + 15, point.Z + 5);
            
            // Draw a line from point to label position
            AddLine(point, labelBase, Colors.White);

            // Draw number using 3D seven-segment style for clarity
            DrawNumber3D(labelBase, index, Colors.White);
        }

        private void DrawNumber3D(Point3D position, int number, Color color)
        {
            string numStr = number.ToString();
            double offsetX = 0;
            double segLen = 12.0;   // length of each segment
            double segGap = 1.5;    // gap between segments

            foreach (char d in numStr)
            {
                // Calculate segment anchor points around a small rectangle
                Point3D topLeft = new Point3D(position.X + offsetX, position.Y + segLen, position.Z + 10);
                Point3D topRight = new Point3D(position.X + offsetX + segLen, position.Y + segLen, position.Z + 10);
                Point3D midLeft = new Point3D(position.X + offsetX, position.Y, position.Z + 10);
                Point3D midRight = new Point3D(position.X + offsetX + segLen, position.Y, position.Z + 10);
                Point3D botLeft = new Point3D(position.X + offsetX, position.Y - segLen, position.Z + 10);
                Point3D botRight = new Point3D(position.X + offsetX + segLen, position.Y - segLen, position.Z + 10);

                // Helper to draw a segment between two points using AddLine (which draws a thin box)
                void Seg(Point3D p1, Point3D p2) => AddLine(p1, p2, color);

                // seven-segment map: a(top), b(upper-right), c(lower-right), d(bottom), e(lower-left), f(upper-left), g(middle)
                bool a=false,b=false,c=false,dSeg=false,e=false,f=false,g=false;
                switch (d)
                {
                    case '0': a=b=c=dSeg=e=f=true; break;
                    case '1': b=c=true; break;
                    case '2': a=b=g=e=dSeg=true; break;
                    case '3': a=b=g=c=dSeg=true; break;
                    case '4': f=g=b=c=true; break;
                    case '5': a=f=g=c=dSeg=true; break;
                    case '6': a=f=g=c=dSeg=e=true; break;
                    case '7': a=b=c=true; break;
                    case '8': a=b=c=dSeg=e=f=g=true; break;
                    case '9': a=b=c=dSeg=f=g=true; break;
                }

                if (a) Seg(new Point3D(topLeft.X, topLeft.Y, topLeft.Z), new Point3D(topRight.X, topRight.Y, topRight.Z));
                if (b) Seg(new Point3D(topRight.X, topRight.Y, topRight.Z), new Point3D(midRight.X, midRight.Y, midRight.Z));
                if (c) Seg(new Point3D(midRight.X, midRight.Y, midRight.Z), new Point3D(botRight.X, botRight.Y, botRight.Z));
                if (dSeg) Seg(new Point3D(botLeft.X, botLeft.Y, botLeft.Z), new Point3D(botRight.X, botRight.Y, botRight.Z));
                if (e) Seg(new Point3D(botLeft.X, botLeft.Y, botLeft.Z), new Point3D(midLeft.X, midLeft.Y, midLeft.Z));
                if (f) Seg(new Point3D(midLeft.X, midLeft.Y, midLeft.Z), new Point3D(topLeft.X, topLeft.Y, topLeft.Z));
                if (g) Seg(new Point3D(midLeft.X, midLeft.Y, midLeft.Z), new Point3D(midRight.X, midRight.Y, midRight.Z));

                offsetX += segLen + segGap;
            }
        }

        private bool IsPointInWorkZone(Point3D point)
        {
            return point.X >= WORK_ZONE_MIN_X && point.X <= WORK_ZONE_MAX_X &&
                   point.Y >= WORK_ZONE_MIN_Y && point.Y <= WORK_ZONE_MAX_Y &&
                   point.Z >= WORK_ZONE_MIN_Z && point.Z <= WORK_ZONE_MAX_Z;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "List files (*.ls)|*.ls|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                LoadProgramFromFile(openFileDialog.FileName);
            }
        }

        private void LoadProgramFromFile(string filePath)
        {
            ProgramListBox.Items.Clear();
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        ProgramListBox.Items.Add(line);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExtractCoordinatesButton_Click(object sender, RoutedEventArgs e)
        {
            string result = "";

            foreach (string item in ProgramListBox.Items)
            {
                int startIndex = item.IndexOf("LINE_COUNT\t=");
                int endIndex = item.IndexOf(";");

                if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                {
                    result = item.Substring(startIndex + "LINE_COUNT\t=".Length, endIndex - (startIndex + "LINE_COUNT\t=".Length)).Trim();
                    MessageBox.Show(result, "Общее число строк в программе:", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            int countlines = 0;
            CoordinatesListBox.Items.Clear();

            foreach (string item in ProgramListBox.Items)
            {
                int index = item.IndexOf("X =   ");
                if (index != -1)
                {
                    countlines++;
                    string value = item.Substring(index).Trim();
                    CoordinatesListBox.Items.Add(value);
                }
            }

            MessageBox.Show(Convert.ToString(countlines), "Число выписанных строк для эмуляции:", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddPointButton_Click(object sender, RoutedEventArgs e)
        {
            
            if (CoordinatesListBox.Items.Count > 0)
            {
                int nextIndex = (CoordinatesListBox.SelectedIndex + 1) % CoordinatesListBox.Items.Count;
                if (nextIndex == 0 && CoordinatesListBox.SelectedIndex >= 0)
                {
                    
                    nextIndex = 0;
                }
                else if (CoordinatesListBox.SelectedIndex < 0)
                {
                    nextIndex = 0;
                }
                
                CoordinatesListBox.SelectedIndex = nextIndex;
                
                if (CoordinatesListBox.SelectedItem != null)
                {
                    string selectedItem = CoordinatesListBox.SelectedItem.ToString();
                    ExtractAndSetCoordinates(selectedItem);
                }
            }
            
            
            if (float.TryParse(XTextBox.Text, out float x) &&
                float.TryParse(YTextBox.Text, out float y) &&
                float.TryParse(ZTextBox.Text, out float z))
            {
                Point3D point = new Point3D(x, y, z);
                points.Add(point);
                
                AddPoint(point, points.Count);
                
                
                if (points.Count >= 2)
                {
                    AddLine(points[points.Count - 2], points[points.Count - 1], Colors.Cyan);
                }
            }
            else
            {
                MessageBox.Show("Введите корректные числа для X, Y, Z.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        
        private void ExtractAndSetCoordinates(string item)
        {
            // Extract X coordinate
            int xIndex = item.IndexOf("X =   ");
            if (xIndex != -1)
            {
                int xEndIndex = item.IndexOf("  mm", xIndex + 6);
                if (xEndIndex > xIndex)
                {
                    string xValue = item.Substring(xIndex + 6, xEndIndex - (xIndex + 6)).Trim();
                    XTextBox.Text = xValue.Replace('.', ',');
                }
            }
            
            // Extract Y coordinate
            int yIndex = item.IndexOf("Y =   ");
            if (yIndex != -1)
            {
                int yEndIndex = item.IndexOf("  mm", yIndex + 6);
                if (yEndIndex > yIndex)
                {
                    string yValue = item.Substring(yIndex + 6, yEndIndex - (yIndex + 6)).Trim();
                    YTextBox.Text = yValue.Replace('.', ',');
                }
            }
            
            // Extract Z coordinate
            int zIndex = item.IndexOf("Z =   ");
            if (zIndex != -1)
            {
                int zEndIndex = item.IndexOf("  mm", zIndex + 6);
                if (zEndIndex > zIndex)
                {
                    string zValue = item.Substring(zIndex + 6, zEndIndex - (zIndex + 6)).Trim();
                    ZTextBox.Text = zValue.Replace('.', ',');
                }
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshScene();
        }

        private void RefreshScene()
        {
            // Save lighting before clearing
            List<Model3D> lights = new List<Model3D>();
            
            foreach (Model3D child in sceneGroup.Children)
            {
                if (child is AmbientLight || child is DirectionalLight)
                {
                    lights.Add(child);
                }
            }
            
            // Clear all children
            sceneGroup.Children.Clear();
            
            // Restore lighting first
            foreach (var light in lights)
            {
                sceneGroup.Children.Add(light);
            }
            
            // Re-initialize base scene (axes, plane, work zone)
            InitializeScene();
            
            // Re-add all points
            for (int i = 0; i < points.Count; i++)
            {
                AddPoint(points[i], i + 1);
            }

            // Draw connecting lines
            if (points.Count >= 2)
            {
                for (int i = 0; i < points.Count - 1; i++)
                {
                    AddLine(points[i], points[i + 1], Colors.Cyan);
                }
            }

            // Re-add loaded 3D models
            foreach (var model in loadedModels)
            {
                if (!sceneGroup.Children.Contains(model))
                {
                    sceneGroup.Children.Add(model);
                }
            }
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

        private Model3DGroup LoadStlAsciiModel(string filePath)
        {
            // Простейший парсер ASCII STL (binary STL не поддерживается)
            var positions = new List<Point3D>();
            var triangleIndices = new List<int>();

            foreach (var line in File.ReadLines(filePath))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("vertex", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        double x = double.Parse(parts[1], CultureInfo.InvariantCulture);
                        double y = double.Parse(parts[2], CultureInfo.InvariantCulture);
                        double z = double.Parse(parts[3], CultureInfo.InvariantCulture);
                        positions.Add(new Point3D(x, y, z));
                        triangleIndices.Add(positions.Count - 1);
                    }
                }
            }

            if (positions.Count == 0 || triangleIndices.Count == 0)
                throw new InvalidDataException("Файл STL не распознан как ASCII STL или не содержит вершин.");

            var mesh = new MeshGeometry3D();
            foreach (var p in positions)
            {
                mesh.Positions.Add(p);
            }
            foreach (var idx in triangleIndices)
            {
                mesh.TriangleIndices.Add(idx);
            }

            var material = new DiffuseMaterial(new SolidColorBrush(Colors.LightGray));
            var geometry = new GeometryModel3D(mesh, material)
            {
                BackMaterial = material
            };

            var group = new Model3DGroup();
            group.Children.Add(geometry);
            return group;
        }

        private Model3DGroup LoadModelByExtension(string filePath)
        {
            var ext = System.IO.Path.GetExtension(filePath)?.ToLowerInvariant();
            switch (ext)
            {
                case ".obj":
                    return LoadObjModel(filePath);
                case ".stl":
                    return LoadStlAsciiModel(filePath);
                case ".fbx":
                    return LoadWithAssimp(filePath);
                default:
                    throw new NotSupportedException($"Формат файла '{ext}' не поддерживается.");
            }
        }

        private Model3DGroup LoadWithAssimp(string filePath)
        {
            var context = new Assimp.AssimpContext();

            
            var scene = context.ImportFile(
                filePath,
                Assimp.PostProcessSteps.Triangulate |
                Assimp.PostProcessSteps.JoinIdenticalVertices |
                Assimp.PostProcessSteps.GenerateNormals |
                Assimp.PostProcessSteps.ImproveCacheLocality);

            if (scene == null || scene.MeshCount == 0)
                throw new InvalidDataException("Assimp не смог импортировать геометрию из файла.");

            var group = new Model3DGroup();

            foreach (var mesh in scene.Meshes)
            {
                var wpfMesh = new MeshGeometry3D();

                
                foreach (var v in mesh.Vertices)
                {
                    wpfMesh.Positions.Add(new Point3D(v.X, v.Y, v.Z));
                }

                
                foreach (var face in mesh.Faces)
                {
                    if (face.IndexCount == 3)
                    {
                        wpfMesh.TriangleIndices.Add(face.Indices[0]);
                        wpfMesh.TriangleIndices.Add(face.Indices[1]);
                        wpfMesh.TriangleIndices.Add(face.Indices[2]);
                    }
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

            return group;
        }

        private void LoadModelButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "3D модели (*.obj;*.stl;*.fbx)|*.obj;*.stl;*.fbx|OBJ (*.obj)|*.obj|STL (*.stl)|*.stl|FBX (*.fbx)|*.fbx|Все файлы (*.*)|*.*"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var model = LoadModelByExtension(dialog.FileName);
                    if (model != null)
                    {
                        // Оборачиваем модель в трансформацию для последующего перемещения
                        var transformGroup = new Transform3DGroup();
                        var translation = new TranslateTransform3D(0, 0, 0);

                        if (model.Transform != null && !(model.Transform is Transform3DGroup existingGroup && existingGroup.Children.Contains(translation)))
                        {
                            transformGroup.Children.Add(model.Transform);
                        }
                        transformGroup.Children.Add(translation);
                        model.Transform = transformGroup;

                        loadedModels.Add(model);
                        lastLoadedModel = model;
                        lastLoadedModelTranslation = translation;

                        sceneGroup.Children.Add(model);
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

        private void ClearModelsButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var model in loadedModels)
            {
                sceneGroup.Children.Remove(model);
            }
            loadedModels.Clear();
            lastLoadedModel = null;
            lastLoadedModelTranslation = new TranslateTransform3D();
        }

        private void ClearSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            // Удаляем все точки из логики и интерфейса и перерисовываем сцену
            points.Clear();
            CoordinatesListBox.Items.Clear();
            XTextBox.Text = string.Empty;
            YTextBox.Text = string.Empty;
            ZTextBox.Text = string.Empty;

            RefreshScene();
        }

        private void ApplyModelOffsetButton_Click(object sender, RoutedEventArgs e)
        {
            if (lastLoadedModel == null || lastLoadedModelTranslation == null)
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
        }

        private void SimulateWindow_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            // Если фокус сейчас в текстовом поле, не перехватываем ввод, чтобы можно было печатать
            if (Keyboard.FocusedElement is TextBox)
            {
                return;
            }

            double moveSpeed = 20.0;
            bool handled = false;

            switch (e.Key)
            {
                case System.Windows.Input.Key.W:
                    cameraTarget.Z -= moveSpeed;
                    handled = true;
                    break;
                case System.Windows.Input.Key.S:
                    cameraTarget.Z += moveSpeed;
                    handled = true;
                    break;
                case System.Windows.Input.Key.A:
                    cameraTarget.X -= moveSpeed;
                    handled = true;
                    break;
                case System.Windows.Input.Key.D:
                    cameraTarget.X += moveSpeed;
                    handled = true;
                    break;
                case System.Windows.Input.Key.Up:
                    angleX -= 5.0;
                    RotationXSlider.Value = angleX;
                    handled = true;
                    break;
                case System.Windows.Input.Key.Down:
                    angleX += 5.0;
                    RotationXSlider.Value = angleX;
                    handled = true;
                    break;
                case System.Windows.Input.Key.Left:
                    angleY -= 5.0;
                    RotationYSlider.Value = angleY;
                    handled = true;
                    break;
                case System.Windows.Input.Key.Right:
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
    }

    // Helper class for creating spheres
    public class Sphere
    {
        private Point3D center;
        private double radius;
        private Color color;

        public Sphere(Point3D center, double radius, Color color)
        {
            this.center = center;
            this.radius = radius;
            this.color = color;
        }


        public Model3D GetModel3D()
        {
            MeshGeometry3D mesh = new MeshGeometry3D();
            int segments = 16;

            for (int i = 0; i <= segments; i++)
            {
                double theta = i * Math.PI / segments;
                for (int j = 0; j <= segments; j++)
                {
                    double phi = j * 2 * Math.PI / segments;
                    double x = center.X + radius * Math.Sin(theta) * Math.Cos(phi);
                    double y = center.Y + radius * Math.Cos(theta);
                    double z = center.Z + radius * Math.Sin(theta) * Math.Sin(phi);
                    mesh.Positions.Add(new Point3D(x, y, z));
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int baseIndex = i * (segments + 1) + j;
                    mesh.TriangleIndices.Add(baseIndex);
                    mesh.TriangleIndices.Add(baseIndex + segments + 1);
                    mesh.TriangleIndices.Add(baseIndex + 1);
                    mesh.TriangleIndices.Add(baseIndex + 1);
                    mesh.TriangleIndices.Add(baseIndex + segments + 1);
                    mesh.TriangleIndices.Add(baseIndex + segments + 2);
                }
            }

            GeometryModel3D geometry = new GeometryModel3D();
            geometry.Geometry = mesh;
            MaterialGroup materialGroup = new MaterialGroup();
            SolidColorBrush brush = new SolidColorBrush(color);
            brush.Freeze();
            materialGroup.Children.Add(new DiffuseMaterial(brush));
            materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(color)));

            geometry.Material = materialGroup;
            geometry.BackMaterial = materialGroup;

            return geometry;
        }
    }
}

