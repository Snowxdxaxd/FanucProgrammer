using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace FanucProgrammer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private bool isXYZMode = false;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void XYZButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PointsCountTextBox.Text))
            {
                MessageBox.Show("Введите количество строк!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isXYZMode = true;
            
            int count = Convert.ToInt32(PointsCountTextBox.Text);
            var points = new ObservableCollection<PointData>();

            for (int i = 1; i <= count; i++)
            {
                points.Add(new PointData
                {
                    Number = i.ToString(),
                    X = "0.000",
                    Y = "0.000",
                    Z = "0.000",
                    W = ".000",
                    P = ".000",
                    R = ".000"
                });
            }

            PointsDataGrid.ItemsSource = points;
            PointsDataGrid.Columns.Clear();
            PointsDataGrid.AutoGenerateColumns = false;
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "#", Binding = new System.Windows.Data.Binding("Number"), Width = 50 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "X, mm", Binding = new System.Windows.Data.Binding("X"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Y, mm", Binding = new System.Windows.Data.Binding("Y"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "Z, mm", Binding = new System.Windows.Data.Binding("Z"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "W, deg", Binding = new System.Windows.Data.Binding("W"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "P, deg", Binding = new System.Windows.Data.Binding("P"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "R, deg", Binding = new System.Windows.Data.Binding("R"), Width = 80 });
        }

        private void JButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PointsCountTextBox.Text))
            {
                MessageBox.Show("Введите количество строк!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isXYZMode = false;
            

            int count = Convert.ToInt32(PointsCountTextBox.Text);
            var points = new ObservableCollection<JointData>();

            for (int i = 1; i <= count; i++)
            {
                points.Add(new JointData
                {
                    Number = i.ToString(),
                    J1 = "0.000",
                    J2 = "0.000",
                    J3 = "0.000",
                    J4 = "0.000",
                    J5 = "0.000",
                    J6 = "0.000"
                });
            }

            PointsDataGrid.ItemsSource = points;
            PointsDataGrid.Columns.Clear();
            PointsDataGrid.AutoGenerateColumns = false;
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "#", Binding = new System.Windows.Data.Binding("Number"), Width = 50 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J1, deg", Binding = new System.Windows.Data.Binding("J1"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J2, deg", Binding = new System.Windows.Data.Binding("J2"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J3, deg", Binding = new System.Windows.Data.Binding("J3"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J4, deg", Binding = new System.Windows.Data.Binding("J4"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J5, deg", Binding = new System.Windows.Data.Binding("J5"), Width = 80 });
            PointsDataGrid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = "J6, deg", Binding = new System.Windows.Data.Binding("J6"), Width = 80 });
        }

        private void GenerateCodeLinesButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(PointsCountTextBox.Text))
            {
                MessageBox.Show("Введите количество строк!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int count = Convert.ToInt32(PointsCountTextBox.Text);
            var pointNames = new ObservableCollection<PointNameData>();

            for (int i = 1; i <= count; i++)
            {
                pointNames.Add(new PointNameData
                {
                    Number = i + ":",
                    PointName = $"P[{i}]"
                });
            }

            PointNamesDataGrid.ItemsSource = pointNames;
        }

        // Метод для расчета PROG_SIZE и MEMORY_SIZE
        private (int progSize, int memorySize) CalculateProgramSizes(int lineCount, int posCount)
        {
            // Базовая формула на основе анализа реальных программ FANUC
            int baseProgSize = 423;
            
            int baseMemorySize = 795;

            // Расчет PROG_SIZE
            

            
            int progSize = baseProgSize + (61 * posCount);

            // Расчет MEMORY_SIZE
            int memorySize = baseMemorySize + (57 * posCount);

            return (progSize, memorySize);
        }

        private void GenerateCodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (PointNamesDataGrid.Items.Count == 0)
            {
                MessageBox.Show("Сначала сгенерируйте строки кода!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int rowCount = PointNamesDataGrid.Items.Count;
            int posCount = PointsDataGrid.Items.Count;

            // Расчет PROG_SIZE и MEMORY_SIZE
            var (progSize, memorySize) = CalculateProgramSizes(rowCount, posCount);

            GeneratedCodeListBox.Items.Clear();

            // Sanitize program name
            string prognameRaw = ProgramNameTextBox.Text ?? string.Empty;
            string progname = new string(prognameRaw.ToUpperInvariant().Select(ch =>
            {
                if ((ch >= 'A' && ch <= 'Z') || (ch >= '0' && ch <= '9') || ch == '_') return ch;
                return '_';
            }).ToArray());
            if (progname.Length > 8) progname = progname.Substring(0, 8);

            string progcom = CommentTextBox.Text ?? "";
            DateTime now = DateTime.Now;
            string createdDate = now.ToString("dd-MM-yy");
            string createdTime = now.ToString("HH:mm:ss");

            // Generate header с правильными PROG_SIZE и MEMORY_SIZE
            GeneratedCodeListBox.Items.Add($"/PROG  {progname}");
            GeneratedCodeListBox.Items.Add("/ATTR");
            GeneratedCodeListBox.Items.Add($"OWNER\t\t= MNEDITOR;");
            GeneratedCodeListBox.Items.Add($"COMMENT\t\t= \"{progcom}\";");
            GeneratedCodeListBox.Items.Add($"PROG_SIZE\t= {progSize};");
            GeneratedCodeListBox.Items.Add($"CREATE\t\t= DATE {createdDate}  TIME {createdTime};");
            GeneratedCodeListBox.Items.Add($"MODIFIED\t= DATE {createdDate}  TIME {createdTime};");
            GeneratedCodeListBox.Items.Add($"FILE_NAME\t= ;");
            GeneratedCodeListBox.Items.Add($"VERSION\t\t= 0;");
            GeneratedCodeListBox.Items.Add($"LINE_COUNT\t= {rowCount};");
            GeneratedCodeListBox.Items.Add($"MEMORY_SIZE\t= {memorySize};");
            GeneratedCodeListBox.Items.Add($"PROTECT\t\t= READ_WRITE;");
            GeneratedCodeListBox.Items.Add($"TCD:  STACK_SIZE\t= 0,");
            GeneratedCodeListBox.Items.Add($"      TASK_PRIORITY\t= 50,");
            GeneratedCodeListBox.Items.Add($"      TIME_SLICE\t= 0,");
            GeneratedCodeListBox.Items.Add($"      BUSY_LAMP_OFF\t= 0,");
            GeneratedCodeListBox.Items.Add($"      ABORT_REQUEST\t= 0,");
            GeneratedCodeListBox.Items.Add($"      PAUSE_REQUEST\t= 0;");
            GeneratedCodeListBox.Items.Add($"DEFAULT_GROUP\t= 1,*,*,*,*;");
            GeneratedCodeListBox.Items.Add($"CONTROL_CODE\t= 00000000 00000000;");
            GeneratedCodeListBox.Items.Add($"LOCAL_REGISTERS\t= 0,0,0;");
            GeneratedCodeListBox.Items.Add("/APPL");
            GeneratedCodeListBox.Items.Add("/APPL");
            GeneratedCodeListBox.Items.Add("");
            GeneratedCodeListBox.Items.Add("AUTO_SINGULARITY_HEADER;");
            GeneratedCodeListBox.Items.Add("  ENABLE_SINGULARITY_AVOIDANCE   : FALSE;");
            
            GeneratedCodeListBox.Items.Add("/MN");

            // Generate movement lines
            foreach (var item in PointNamesDataGrid.Items)
            {
                if (item is PointNameData pointName)
                {
                    string number = pointName.Number?.Replace(":", "") ?? "1";
                    string namepoint = pointName.PointName ?? "P[1]";
                    GeneratedCodeListBox.Items.Add($"   {number}:L {namepoint} 250mm/sec FINE    ;");
                }
            }

            GeneratedCodeListBox.Items.Add("/POS");

            // Generate position data
            foreach (var item in PointsDataGrid.Items)
            {
                if (isXYZMode && item is PointData point)
                {
                    string number = point.Number ?? "1";
                    string x = point.X ?? "0.000";
                    string y = point.Y ?? "0.000";
                    string z = point.Z ?? "0.000";
                    string w = point.W ?? ".000";
                    string p = point.P ?? ".000";
                    string r = point.R ?? ".000";

                    GeneratedCodeListBox.Items.Add($"P[{number}]{{");
                    GeneratedCodeListBox.Items.Add("   GP1:");
                    GeneratedCodeListBox.Items.Add($"\tUF : 1, UT : 1,\t\tCONFIG : 'N U T, 0, 0, 0',");
                    GeneratedCodeListBox.Items.Add($"\tX =   {x}  mm,\tY =   {y}  mm,\tZ =    {z}  mm,");
                    GeneratedCodeListBox.Items.Add($"\tW = {w} deg,\tP =      {p} deg,\tR =    {r} deg");
                    GeneratedCodeListBox.Items.Add("};");
                }
                else if (!isXYZMode && item is JointData joint)
                {
                    string number = joint.Number ?? "1";
                    string j1 = joint.J1 ?? "0.000";
                    string j2 = joint.J2 ?? "0.000";
                    string j3 = joint.J3 ?? "0.000";
                    string j4 = joint.J4 ?? "0.000";
                    string j5 = joint.J5 ?? "0.000";
                    string j6 = joint.J6 ?? "0.000";

                    GeneratedCodeListBox.Items.Add($"P[{number}]{{");
                    GeneratedCodeListBox.Items.Add("   GP1:");
                    GeneratedCodeListBox.Items.Add($"\tUF : 1, UT : 1,");
                    GeneratedCodeListBox.Items.Add($"\tJ1=     {j1} deg,\tJ2=     {j2} deg,\tJ3=     {j3} deg,");
                    GeneratedCodeListBox.Items.Add($"\tJ4=     {j4} deg,\tJ5=     {j5} deg,\tJ6=     {j6} deg");
                    GeneratedCodeListBox.Items.Add("};");
                }
            }

            GeneratedCodeListBox.Items.Add("/END");
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string filename = ProgramNameTextBox.Text;
            if (string.IsNullOrEmpty(filename))
            {
                MessageBox.Show("Введите название программы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "List files (*.ls)|*.ls|All files (*.*)|*.*",
                FileName = filename
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(saveFileDialog.FileName, false, System.Text.Encoding.ASCII))
                    {
                        foreach (var item in GeneratedCodeListBox.Items)
                        {
                            sw.WriteLine(item.ToString());
                        }
                    }

                    MessageBox.Show("Данные успешно сохранены в файл: " + saveFileDialog.FileName, "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Ошибка при сохранении данных: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SimulateMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SimulateWindow simulateWindow = new SimulateWindow();
            simulateWindow.Show();
        }

        private void PointsOnModelMenuItem_Click(object sender, RoutedEventArgs e)
        {
            PointsOnModelWindow window = new PointsOnModelWindow();
            window.Show();
        }

        private void TextToPointsGenerator_Click(object sender, RoutedEventArgs e)
        {
            TextToPointsWindow window = new TextToPointsWindow();
            window.Show();
        }

        private void FanucTextGenerator_Click(object sender, RoutedEventArgs e)
        {
            AutoLetterArrangementWindow window = new AutoLetterArrangementWindow();
            window.Show();
        }

    }

    // Data Models
    public class PointData
    {
        public string Number { get; set; }
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }
        public string W { get; set; }
        public string P { get; set; }
        public string R { get; set; }
    }

    public class JointData
    {
        public string Number { get; set; }
        public string J1 { get; set; }
        public string J2 { get; set; }
        public string J3 { get; set; }
        public string J4 { get; set; }
        public string J5 { get; set; }
        public string J6 { get; set; }
    }

    public class PointNameData
    {
        public string Number { get; set; }
        public string PointName { get; set; }
    }
}