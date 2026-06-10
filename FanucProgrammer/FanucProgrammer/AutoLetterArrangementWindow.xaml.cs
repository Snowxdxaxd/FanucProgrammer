using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;

using SystemPath = System.IO.Path;

namespace FanucProgrammer
{
    public partial class AutoLetterArrangementWindow : Window
    {
        private Dictionary<char, LetterTemplate> _letterLibrary = new Dictionary<char, LetterTemplate>();

        private const double MARGIN = 10.0;
        private double _currentSheetMaxX = 100.0;
        private double _currentSheetMinX = -130.0;
        private double _currentSheetMaxY = 65.0;
        private double _currentSheetMinY = -175.0;

        private const double SHEET_WIDTH = 210.0;
        private const double SHEET_HEIGHT = 297.0;

        private const int BASE_PROG_SIZE = 423;
        private const int BASE_MEMORY_SIZE = 795;
        private const int SIZE_PER_POINT = 61;
        private const int MEMORY_PER_POINT = 57;
        private const string RUSSIAN_ALPHABET = "АБВГДЕЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯЁ";
        private const double SPACE_WIDTH = 40.0;

        public class LetterTemplate
        {
            public char Character { get; set; }
            public List<ProgramPoint> RelativePoints { get; set; } = new List<ProgramPoint>();
            public Point FirstPointRel { get; set; }   // нормализованные координаты первой точки
            public Point LastPointRel { get; set; }    // нормализованные координаты последней точки
            public double Width { get; set; }
            public double Height { get; set; }
            public double BaseZ { get; set; }
            public double RetractDelta { get; set; }
        }

        public class ProgramPoint
        {
            public Point3D Point { get; set; }
            public double R { get; set; }
        }

        public AutoLetterArrangementWindow()
        {
            InitializeComponent();
            LoadLetterTemplates();
            DrawSheetGrid();

            Console.WriteLine($"Загружено шаблонов: {_letterLibrary.Count}");
        }

        private void Settings_Changed(object sender, EventArgs e)
        {
            if (SheetCanvas == null) return;
            UpdatePreview();
        }

        private void LoadLetterTemplates()
        {
            string latin = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            foreach (char c in latin)
            {
                var template = ParseLsFile($"{c}.LS");
                if (template != null) _letterLibrary[c] = template;
                else Console.WriteLine($"Не удалось загрузить латинскую букву: {c}");
            }

            foreach (char c in RUSSIAN_ALPHABET)
            {
                var template = ParseLsFile($"{c}.LS");
                if (template != null) _letterLibrary[c] = template;
                else Console.WriteLine($"Не удалось загрузить кириллическую букву: {c}");
            }

            MessageBox.Show($"Загружено букв: {_letterLibrary.Count} (латиница + кириллица)");
        }

        private void DrawSheetGrid()
        {
            GeometryGroup gridGroup = new GeometryGroup();
            for (int x = 0; x <= SHEET_WIDTH; x += 10)
                gridGroup.Children.Add(new LineGeometry(new Point(x, 0), new Point(x, SHEET_HEIGHT)));
            for (int y = 0; y <= SHEET_HEIGHT; y += 10)
                gridGroup.Children.Add(new LineGeometry(new Point(0, y), new Point(SHEET_WIDTH, y)));
            GridPath.Data = gridGroup;
        }

        private void UpdatePreview()
        {
            SheetCanvas.Children.Clear();
            DrawSheetGrid();

            string text = TxtInput.Text.ToUpper();
            double scale = SldScale.Value;
            double lineSpacing = SldLineSpacing.Value;
            double charSpacing = SldCharSpacing.Value;

            double cursorX = MARGIN;
            double cursorY = MARGIN;

            foreach (char c in text)
            {
                if (c == ' ')
                {
                    cursorX += SPACE_WIDTH * scale;
                    continue;
                }
                if (c == '\n')
                {
                    cursorX = MARGIN;
                    cursorY += lineSpacing;
                    continue;
                }
                if (!_letterLibrary.ContainsKey(c)) continue;

                var letter = _letterLibrary[c];
                if (cursorX + letter.Width * scale > SHEET_WIDTH - MARGIN)
                {
                    cursorX = MARGIN;
                    cursorY += lineSpacing;
                }

                var poly = new Polyline
                {
                    Stroke = Brushes.DarkBlue,
                    StrokeThickness = 0.8
                };

                foreach (var p in letter.RelativePoints)
                {
                    poly.Points.Add(new Point(
                        cursorX + p.Point.X * scale,
                        cursorY + (letter.Height - p.Point.Y) * scale
                    ));
                }
                SheetCanvas.Children.Add(poly);
                cursorX += (letter.Width * scale) + charSpacing;
            }
        }

        private void ExportTextToFanucLS(
            List<ProgramPoint> programPoints,
            string programName,
            string programComment,
            string speed,
            string motionType)
        {
            if (programPoints == null || programPoints.Count == 0)
            {
                MessageBox.Show("Нет точек для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CalculateFanucSizes(programPoints.Count, out int progSize, out int memorySize, out int lineCount);

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Fanuc LS (*.ls)|*.ls",
                FileName = programName,
                DefaultExt = ".ls"
            };

            if (saveDialog.ShowDialog() != true) return;

            using (StreamWriter sw = new StreamWriter(saveDialog.FileName, false, Encoding.ASCII))
            {
                DateTime now = DateTime.Now;
                string date = now.ToString("dd-MM-yy");
                string time = now.ToString("HH:mm:ss");

                sw.WriteLine($"/PROG  {programName}");
                sw.WriteLine("/ATTR");
                sw.WriteLine("OWNER\t\t= MNEDITOR;");
                sw.WriteLine($"COMMENT\t\t= \"{programComment}\";");
                sw.WriteLine($"PROG_SIZE\t= {progSize};");
                sw.WriteLine($"CREATE\t\t= DATE {date}  TIME {time};");
                sw.WriteLine($"MODIFIED\t= DATE {date}  TIME {time};");
                sw.WriteLine("FILE_NAME\t= ;");
                sw.WriteLine("VERSION\t\t= 0;");
                sw.WriteLine($"LINE_COUNT\t= {lineCount};");
                sw.WriteLine($"MEMORY_SIZE\t= {memorySize};");
                sw.WriteLine("PROTECT\t\t= READ_WRITE;");
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
                    sw.WriteLine($"   {i + 1}:L P[{i + 1}] {speed}mm/sec {motionType} ;");

                sw.WriteLine("/POS");

                for (int i = 0; i < programPoints.Count; i++)
                {
                    var p = programPoints[i].Point;
                    string rFormatted = programPoints[i].R.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(8);

                    sw.WriteLine($"P[{i + 1}]{{");
                    sw.WriteLine("   GP1:");
                    sw.WriteLine("    UF : 9, UT : 10, CONFIG : 'N U T, 0, 0, 0',");
                    sw.WriteLine(
                        $"    X = {p.X.ToString("0.000", CultureInfo.InvariantCulture)} mm," +
                        $" Y = {p.Y.ToString("0.000", CultureInfo.InvariantCulture)} mm," +
                        $" Z = {p.Z.ToString("0.000", CultureInfo.InvariantCulture)} mm,");
                    sw.WriteLine($"    W = -180.000 deg, P = 0.000 deg, R = {rFormatted} deg");
                    sw.WriteLine("};");
                }
                sw.WriteLine("/END");
            }

            MessageBox.Show($"Программа '{programName}' создана успешно", "Fanuc", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnGenerate_Click(object sender, RoutedEventArgs e)
        {
            var programPoints = GenerateWordPoints(
                TxtInput.Text,
                SldScale.Value,
                SldLineSpacing.Value,
                SldCharSpacing.Value,
                SldZ.Value,
                SldZRetract.Value);

            ExportTextToFanucLS(programPoints, "TEXT", "", "250", "CNT60");
        }

        // ОСНОВНАЯ ФУНКЦИЯ ГЕНЕРАЦИИ КООРДИНАТ С ПРАВИЛЬНЫМИ ПЕРЕХОДАМИ
        public List<ProgramPoint> GenerateWordPoints(
            string text,
            double scale,
            double lineSpacing,
            double charSpacing,
            double zWrite,
            double zRetract)
        {
            var result = new List<ProgramPoint>();

            double cursorX = _currentSheetMinX + MARGIN;
            double cursorY = _currentSheetMaxY - MARGIN;
            Point? prevLastGlobal = null; // последняя точка предыдущей буквы (глобальные координаты)

            foreach (char c in text.ToUpper())
            {
                if (c == ' ')
                {
                    cursorX += SPACE_WIDTH * scale;
                    continue;
                }

                if (c == '\n')
                {
                    cursorX = _currentSheetMinX + MARGIN;
                    cursorY -= lineSpacing;
                    continue;
                }

                if (!_letterLibrary.ContainsKey(c)) continue;
                var letter = _letterLibrary[c];

                // Проверка переноса строки по ширине
                if (cursorX + letter.Width * scale > _currentSheetMaxX - MARGIN)
                {
                    cursorX = _currentSheetMinX + MARGIN;
                    cursorY -= lineSpacing;
                    // перенос строки не добавляет точек, просто меняем позицию
                }

                // Глобальные координаты первой и последней точек буквы
                Point firstGlobal = new Point(
                    cursorX + letter.FirstPointRel.X * scale,
                    cursorY + letter.FirstPointRel.Y * scale);
                Point lastGlobal = new Point(
                    cursorX + letter.LastPointRel.X * scale,
                    cursorY + letter.LastPointRel.Y * scale);

                // --- Переход от предыдущей буквы (или старта) к первой точке текущей ---
                if (prevLastGlobal == null)
                {
                    // Первая буква: подъём в начальной позиции, перемещение к первой точке, опускание
                    result.Add(new ProgramPoint { Point = new Point3D(cursorX, cursorY, zRetract), R = 0 });
                    result.Add(new ProgramPoint { Point = new Point3D(firstGlobal.X, firstGlobal.Y, zRetract), R = 0 });
                    result.Add(new ProgramPoint { Point = new Point3D(firstGlobal.X, firstGlobal.Y, zWrite), R = 0 });
                }
                else
                {
                    // Переход от последней точки предыдущей буквы
                    result.Add(new ProgramPoint { Point = new Point3D(prevLastGlobal.Value.X, prevLastGlobal.Value.Y, zRetract), R = 0 });
                    result.Add(new ProgramPoint { Point = new Point3D(firstGlobal.X, firstGlobal.Y, zRetract), R = 0 });
                    result.Add(new ProgramPoint { Point = new Point3D(firstGlobal.X, firstGlobal.Y, zWrite), R = 0 });
                }

                // --- Добавляем все точки буквы с корректировкой Z ---
                foreach (var p in letter.RelativePoints)
                {
                    double z = Math.Abs(p.Point.Z - letter.BaseZ) < 0.001 ? zWrite : zRetract;
                    result.Add(new ProgramPoint
                    {
                        Point = new Point3D(
                            cursorX + p.Point.X * scale,
                            cursorY + p.Point.Y * scale,
                            z),
                        R = p.R
                    });
                }

                // Запоминаем последнюю точку для следующего перехода
                prevLastGlobal = lastGlobal;

                // Сдвигаем курсор для следующей буквы
                cursorX += (letter.Width * scale) + charSpacing;
            }

            // Финальный подъём в конце текста
            if (prevLastGlobal != null)
                result.Add(new ProgramPoint { Point = new Point3D(prevLastGlobal.Value.X, prevLastGlobal.Value.Y, zRetract), R = 0 });

            return result;
        }

        private void CalculateFanucSizes(int pointCount, out int progSize, out int memorySize, out int lineCount)
        {
            progSize = BASE_PROG_SIZE + (SIZE_PER_POINT * pointCount);
            memorySize = BASE_MEMORY_SIZE + (MEMORY_PER_POINT * pointCount);
            lineCount = pointCount;
        }

        // ─────────────────────────────────────────────
        //  KUKA EXPORT
        // ─────────────────────────────────────────────

        private void BtnExportKuka_Click(object sender, RoutedEventArgs e)
        {
            double zWrite   = SldZ.Value;
            double zRetract = SldZRetract.Value;

            var points = GenerateWordPoints(
                TxtInput.Text,
                SldScale.Value,
                SldLineSpacing.Value,
                SldCharSpacing.Value,
                zWrite,
                zRetract);

            if (points == null || points.Count == 0)
            {
                MessageBox.Show("Нет точек для экспорта. Введите текст.", "KUKA Экспорт",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExportToKuka(points, "TEXT", TxtInput.Text.ToUpper(), zWrite, zRetract);
        }

        private static void WriteKukaHomeBlock(StreamWriter sw)
        {
            sw.WriteLine(";FOLD SPTP HOME Vel=100 % DEFAULT ;%{PE}");
            sw.WriteLine(";FOLD Parameters ;%{h}");
            sw.WriteLine(";Params IlfProvider=kukaroboter.basistech.inlineforms.movement.spline; Kuka.IsGlobalPoint=False; Kuka.PointName=HOME; Kuka.BlendingEnabled=False; Kuka.MoveDataPtpName=DEFAULT; Kuka.VelocityPtp=100; Kuka.VelocityFieldEnabled=True; Kuka.CurrentCDSetIndex=0; Kuka.MovementParameterFieldEnabled=True; IlfCommand=SPTP");
            sw.WriteLine(";ENDFOLD");
            sw.WriteLine("SPTP XHOME WITH $VEL_AXIS[1] = SVEL_JOINT(100.0), $TOOL = STOOL2(FHOME), $BASE = SBASE(FHOME.BASE_NO), $IPO_MODE = SIPO_MODE(FHOME.IPO_FRAME), $LOAD = SLOAD(FHOME.TOOL_NO), $ACC_AXIS[1] = SACC_JOINT(PDEFAULT), $APO = SAPO_PTP(PDEFAULT), $GEAR_JERK[1] = SGEAR_JERK(PDEFAULT), $COLLMON_TOL_PRO[1] = USE_CM_PRO_VALUES(0)");
            sw.WriteLine(";ENDFOLD");
        }

        private void ExportToKuka(
            List<ProgramPoint> points,
            string programName,
            string text,
            double zWrite,
            double zRetract)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter     = "KUKA SRC (*.src)|*.src",
                FileName   = programName,
                DefaultExt = ".src",
                Title      = "Сохранить KUKA программу"
            };

            if (saveDialog.ShowDialog() != true) return;

            string srcPath = saveDialog.FileName;
            string datPath = SystemPath.ChangeExtension(srcPath, ".dat");

            string progName = SystemPath.GetFileNameWithoutExtension(srcPath).ToUpper();
            if (progName.Length > 24) progName = progName.Substring(0, 24);

            // ── Classify every point ──────────────────────────────────────
            const int KIND_PTP      = 0;
            const int KIND_APPROACH = 1;
            const int KIND_WRITE    = 2;

            var kinds = new int[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                bool atRetract     = Math.Abs(points[i].Point.Z - zRetract) < 0.001;
                bool prevAtRetract = i == 0 || Math.Abs(points[i - 1].Point.Z - zRetract) < 0.001;

                if (atRetract && (i == 0 || prevAtRetract))
                    kinds[i] = KIND_PTP;
                else if (atRetract)
                    kinds[i] = KIND_APPROACH;
                else
                    kinds[i] = KIND_WRITE;
            }

            // Separate sequential indices for PDAT (SPTP) and CPDAT (LIN)
            var ptpIdx = new int[points.Count];
            var linIdx = new int[points.Count];
            int ptpCounter = 0, linCounter = 0;
            for (int i = 0; i < points.Count; i++)
            {
                if (kinds[i] == KIND_PTP) ptpIdx[i] = ++ptpCounter;
                else                      linIdx[i]  = ++linCounter;
            }

            // ════════════════════════════════════════════════════════════
            //  SRC file
            // ════════════════════════════════════════════════════════════
            using (var sw = new StreamWriter(srcPath, false, Encoding.ASCII))
            {
                sw.WriteLine("&ACCESS RV");
                sw.WriteLine("&REL 2");
                sw.WriteLine("&PARAM EDITMASK = *");
                sw.WriteLine("&PARAM TEMPLATE = C:\\KRC\\Roboter\\Template\\vorgabe");
                sw.WriteLine("&PARAM DISKPATH = KRC:\\R1\\GTT");
                sw.WriteLine($"DEF {progName}( )");
                sw.WriteLine(";FOLD INI;%{PE}");
                sw.WriteLine("  ;FOLD BASISTECH INI");
                sw.WriteLine("    GLOBAL INTERRUPT DECL 3 WHEN $STOPMESS==TRUE DO IR_STOPM ( )");
                sw.WriteLine("    INTERRUPT ON 3 ");
                sw.WriteLine("    BAS (#INITMOV,0 )");
                sw.WriteLine("  ;ENDFOLD (BASISTECH INI)");
                sw.WriteLine("  ;FOLD USER INI");
                sw.WriteLine("    ;Make your modifications here");
                sw.WriteLine();
                sw.WriteLine("  ;ENDFOLD (USER INI)");
                sw.WriteLine(";ENDFOLD (INI)");
                sw.WriteLine();

                WriteKukaHomeBlock(sw);
                sw.WriteLine();

                for (int i = 0; i < points.Count; i++)
                {
                    int n = i + 1;
                    if (kinds[i] == KIND_PTP)
                    {
                        int pi = ptpIdx[i];
                        sw.WriteLine($";FOLD SPTP P{n} Vel=100 % PDAT{pi} Tool[8]:GORELKA Base[12] ;%{{PE}}");
                        sw.WriteLine(";FOLD Parameters ;%{h}");
                        sw.WriteLine($";Params IlfProvider=kukaroboter.basistech.inlineforms.movement.spline; Kuka.IsGlobalPoint=False; Kuka.PointName=P{n}; Kuka.BlendingEnabled=False; Kuka.MoveDataPtpName=PDAT{pi}; Kuka.VelocityPtp=100; Kuka.VelocityFieldEnabled=True; Kuka.ColDetectFieldEnabled=True; Kuka.CurrentCDSetIndex=0; Kuka.MovementParameterFieldEnabled=True; IlfCommand=SPTP");
                        sw.WriteLine(";ENDFOLD");
                        sw.WriteLine($"SPTP XP{n} WITH $VEL_AXIS[1] = SVEL_JOINT(100.0), $TOOL = STOOL2(FP{n}), $BASE = SBASE(FP{n}.BASE_NO), $IPO_MODE = SIPO_MODE(FP{n}.IPO_FRAME), $LOAD = SLOAD(FP{n}.TOOL_NO), $ACC_AXIS[1] = SACC_JOINT(PPDAT{pi}), $APO = SAPO_PTP(PPDAT{pi}), $GEAR_JERK[1] = SGEAR_JERK(PPDAT{pi}), $COLLMON_TOL_PRO[1] = USE_CM_PRO_VALUES(0)");
                        sw.WriteLine(";ENDFOLD");
                    }
                    else
                    {
                        int li = linIdx[i];
                        double vel = (kinds[i] == KIND_APPROACH) ? 0.5 : 0.25;
                        string velStr = vel.ToString("0.##", CultureInfo.InvariantCulture);
                        sw.WriteLine($";FOLD LIN P{n} Vel={velStr} m/s CPDAT{li} Tool[8]:GORELKA Base[12] ;%{{PE}}");
                        sw.WriteLine(";FOLD Parameters ;%{h}");
                        sw.WriteLine($";Params IlfProvider=kukaroboter.basistech.inlineforms.movement.old; Kuka.IsGlobalPoint=False; Kuka.PointName=P{n}; Kuka.BlendingEnabled=False; Kuka.MoveDataName=CPDAT{li}; Kuka.VelocityPath={velStr}; Kuka.CurrentCDSetIndex=0; Kuka.MovementParameterFieldEnabled=True; IlfCommand=LIN");
                        sw.WriteLine(";ENDFOLD");
                        sw.WriteLine("$BWDSTART = FALSE");
                        sw.WriteLine($"LDAT_ACT = LCPDAT{li}");
                        sw.WriteLine($"FDAT_ACT = FP{n}");
                        sw.WriteLine($"BAS(#CP_PARAMS, {velStr})");
                        sw.WriteLine("SET_CD_PARAMS (0)");
                        sw.WriteLine($"LIN XP{n}");
                        sw.WriteLine(";ENDFOLD");
                    }
                }

                sw.WriteLine();
                WriteKukaHomeBlock(sw);
                sw.WriteLine();
                sw.WriteLine("END");
            }

            // ════════════════════════════════════════════════════════════
            //  DAT file
            // ════════════════════════════════════════════════════════════
            using (var sw = new StreamWriter(datPath, false, Encoding.ASCII))
            {
                sw.WriteLine("&ACCESS RV");
                sw.WriteLine("&REL 2");
                sw.WriteLine("&PARAM EDITMASK = *");
                sw.WriteLine("&PARAM TEMPLATE = C:\\KRC\\Roboter\\Template\\vorgabe");
                sw.WriteLine("&PARAM DISKPATH = KRC:\\R1\\GTT");
                sw.WriteLine($"DEFDAT  {progName}");
                sw.WriteLine(";FOLD EXTERNAL DECLARATIONS;%{PE}%MKUKATPBASIS,%CEXT,%VCOMMON,%P");
                sw.WriteLine(";FOLD BASISTECH EXT;%{PE}%MKUKATPBASIS,%CEXT,%VEXT,%P");
                sw.WriteLine("EXT  BAS (BAS_COMMAND  :IN,REAL  :IN )");
                sw.WriteLine("DECL INT SUCCESS");
                sw.WriteLine(";ENDFOLD (BASISTECH EXT)");
                sw.WriteLine(";FOLD USER EXT;%{E}%MKUKATPUSER,%CEXT,%VEXT,%P");
                sw.WriteLine(";Make your modifications here");
                sw.WriteLine();
                sw.WriteLine(";ENDFOLD (USER EXT)");
                sw.WriteLine(";ENDFOLD (EXTERNAL DECLARATIONS)");

                for (int i = 0; i < points.Count; i++)
                {
                    int    n  = i + 1;
                    var    pt = points[i].Point;
                    double a  = points[i].R;
                    // B and C angles match the physical robot cell orientation from the template
                    double b  = 52.1911201;
                    double c  = 177.483353;

                    string px = pt.X.ToString("0.######", CultureInfo.InvariantCulture);
                    string py = pt.Y.ToString("0.######", CultureInfo.InvariantCulture);
                    string pz = pt.Z.ToString("0.######", CultureInfo.InvariantCulture);
                    string pa = a.ToString("0.######", CultureInfo.InvariantCulture);
                    string pb = b.ToString("0.######", CultureInfo.InvariantCulture);
                    string pc = c.ToString("0.######", CultureInfo.InvariantCulture);

                    sw.WriteLine($"DECL E6POS XP{n}={{X {px},Y {py},Z {pz},A {pa},B {pb},C {pc},S 2,T 10,E1 0.0,E2 0.0,E3 0.0,E4 0.0,E5 0.0,E6 0.0}}");
                    sw.WriteLine($"DECL FDAT FP{n}={{TOOL_NO 8,BASE_NO 12,IPO_FRAME #BASE,POINT2[] \" \"}}");

                    if (kinds[i] == KIND_PTP)
                    {
                        sw.WriteLine($"DECL PDAT PPDAT{ptpIdx[i]}={{VEL 100.000,ACC 100.000,APO_DIST 500.000,APO_MODE #CDIS,GEAR_JERK 100.000,EXAX_IGN 0}}");
                    }
                    else
                    {
                        double vel = (kinds[i] == KIND_APPROACH) ? 0.5 : 0.25;
                        string lv  = vel.ToString("0.00000", CultureInfo.InvariantCulture);
                        sw.WriteLine($"DECL LDAT LCPDAT{linIdx[i]}={{VEL {lv},ACC 100.000,APO_DIST 500.000,APO_FAC 50.0000,AXIS_VEL 100.000,AXIS_ACC 100.000,ORI_TYP #VAR,CIRC_TYP #BASE,JERK_FAC 50.0000,GEAR_JERK 100.000,EXAX_IGN 0}}");
                    }
                }

                // LAST_TP_PARAMS – WorkVisual inline-form metadata for the last motion
                int    lastI    = points.Count - 1;
                int    lastN    = points.Count;
                string lastCmd  = (kinds[lastI] == KIND_PTP) ? "SPTP" : "LIN";
                string lastMov  = (kinds[lastI] == KIND_PTP)
                    ? $"PDAT{ptpIdx[lastI]}"
                    : $"CPDAT{linIdx[lastI]}";
                double lastVel  = (kinds[lastI] == KIND_WRITE) ? 0.25 : (kinds[lastI] == KIND_APPROACH ? 0.5 : 0.0);
                string lastVelS = lastVel.ToString("0.##", CultureInfo.InvariantCulture);
                sw.WriteLine(
                    $"DECL MODULEPARAM_T LAST_TP_PARAMS={{PARAMS[] \"Kuka.VelocityFieldEnabled=False; " +
                    $"Kuka.ColDetectFieldEnabled=False; Kuka.MovementParameterFieldEnabled=False; " +
                    $"Kuka.IsAngleEnabled=False; Kuka.PointName=P{lastN}; Kuka.FrameData.base_no=12; " +
                    $"Kuka.FrameData.tool_no=8; Kuka.FrameData.ipo_frame=#BASE; Kuka.isglobalpoint=False; " +
                    $"Kuka.BlendingEnabled=False; Kuka.CurrentCDSetIndex=0; Kuka.MoveDataName={lastMov}; " +
                    $"Kuka.VelocityPath={lastVelS}; IlfCommand={lastCmd}\"}}");

                sw.WriteLine("ENDDAT");
            }

            MessageBox.Show(
                $"KUKA программа '{progName}' создана успешно!\n\n" +
                $"SRC: {srcPath}\n" +
                $"DAT: {datPath}\n\n" +
                $"Точек: {points.Count}",
                "KUKA Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─────────────────────────────────────────────
        //  ГТТ EXPORT  (Fanuc LS, UF:1 UT:1, FINE)
        // ─────────────────────────────────────────────

        private void BtnExportGtt_Click(object sender, RoutedEventArgs e)
        {
            var points = GenerateWordPoints(
                TxtInput.Text,
                SldScale.Value,
                SldLineSpacing.Value,
                SldCharSpacing.Value,
                SldZ.Value,
                SldZRetract.Value);

            if (points == null || points.Count == 0)
            {
                MessageBox.Show("Нет точек для экспорта. Введите текст.", "ГТТ",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExportToGttFanucLS(points, "TEXT", TxtInput.Text.ToUpper());
        }

        /// <summary>
        /// Generates a Fanuc LS file in the ГТТ format:
        ///   UF : 1, UT : 1
        ///   W = -179.788 deg, P = -2.003 deg (fixed)
        ///   Speed: 200 mm/sec FINE
        ///   No AUTO_SINGULARITY_HEADER
        ///
        /// Size formulae derived from real ГТТ programs:
        ///   BASE_PROG_SIZE   = 392  (+61 per point)
        ///   BASE_MEMORY_SIZE = 764  (+57 per point)
        /// </summary>
        private void ExportToGttFanucLS(
            List<ProgramPoint> points,
            string programName,
            string commentText)
        {
            if (points == null || points.Count == 0)
            {
                MessageBox.Show("Нет точек для экспорта", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            const int GTT_BASE_PROG   = 392;
            const int GTT_BASE_MEM    = 764;
            const int GTT_PER_POINT_PROG = 61;
            const int GTT_PER_POINT_MEM  = 57;

            int lineCount  = points.Count;
            int progSize   = GTT_BASE_PROG + GTT_PER_POINT_PROG * lineCount;
            int memorySize = GTT_BASE_MEM  + GTT_PER_POINT_MEM  * lineCount;

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter      = "Fanuc LS (*.ls)|*.ls",
                FileName    = programName,
                DefaultExt  = ".ls",
                Title       = "Сохранить ГТТ программу"
            };

            if (saveDialog.ShowDialog() != true) return;

            DateTime now = DateTime.Now;
            string date  = now.ToString("yy-MM-dd");
            string time  = now.ToString("HH:mm:ss");

            using (var sw = new StreamWriter(saveDialog.FileName, false, Encoding.ASCII))
            {
                sw.WriteLine($"/PROG  {programName}");
                sw.WriteLine("/ATTR");
                sw.WriteLine("OWNER\t\t= MNEDITOR;");
                sw.WriteLine($"COMMENT\t\t= \"\";");
                sw.WriteLine($"PROG_SIZE\t= {progSize};");
                sw.WriteLine($"CREATE\t\t= DATE {date}  TIME {time};");
                sw.WriteLine($"MODIFIED\t= DATE {date}  TIME {time};");
                sw.WriteLine("FILE_NAME\t= ;");
                sw.WriteLine("VERSION\t\t= 0;");
                sw.WriteLine($"LINE_COUNT\t= {lineCount};");
                sw.WriteLine($"MEMORY_SIZE\t= {memorySize};");
                sw.WriteLine("PROTECT\t\t= READ_WRITE;");
                sw.WriteLine("TCD:  STACK_SIZE\t= 0,");
                sw.WriteLine("      TASK_PRIORITY\t= 50,");
                sw.WriteLine("      TIME_SLICE\t= 0,");
                sw.WriteLine("      BUSY_LAMP_OFF\t= 0,");
                sw.WriteLine("      ABORT_REQUEST\t= 0,");
                sw.WriteLine("      PAUSE_REQUEST\t= 0;");
                sw.WriteLine("DEFAULT_GROUP\t= 1,*,*,*,*;");
                sw.WriteLine("CONTROL_CODE\t= 00000000 00000000;");
                sw.WriteLine("/MN");

                for (int i = 0; i < points.Count; i++)
                    sw.WriteLine($"   {i + 1}:L P[{i + 1}] 200mm/sec CNT60    ;");

                sw.WriteLine("/POS");

                for (int i = 0; i < points.Count; i++)
                {
                    var pt = points[i].Point;
                    string x = pt.X.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);
                    string y = pt.Y.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);
                    string z = pt.Z.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);
                    string r = points[i].R.ToString("0.000", CultureInfo.InvariantCulture).PadLeft(9);

                    sw.WriteLine($"P[{i + 1}]{{");
                    sw.WriteLine("   GP1:");
                    sw.WriteLine($"    UF : 8, UT : 1,\t\tCONFIG : 'N U T, 0, 0, 0',");
                    sw.WriteLine($"    X = {x}  mm,\tY = {y}  mm,\tZ = {z}  mm,");
                    sw.WriteLine($"    W =      -173.648 deg,\tP =   -1.333 deg,\tR = {r} deg");
                    sw.WriteLine("};");
                }

                sw.WriteLine("/END");
            }

            MessageBox.Show(
                $"ГТТ программа '{programName}' создана успешно!\n" +
                $"Точек: {lineCount}   PROG_SIZE: {progSize}   MEMORY_SIZE: {memorySize}",
                "ГТТ", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private LetterTemplate ParseLsFile(string fileName)
        {
            try
            {
                string[] possibleExtensions = { ".LS", ".ls" };
                string fullPath = null;

                foreach (var ext in possibleExtensions)
                {
                    string path = SystemPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates",
                        fileName.Replace(".LS", ext).Replace(".ls", ext));
                    if (File.Exists(path))
                    {
                        fullPath = path;
                        break;
                    }
                }

                if (fullPath == null)
                {
                    string templatesPath = SystemPath.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates");
                    if (Directory.Exists(templatesPath))
                    {
                        var files = Directory.GetFiles(templatesPath, "*.*", SearchOption.TopDirectoryOnly);
                        string targetFile = fileName.Replace(".LS", "").Replace(".ls", "");
                        foreach (var file in files)
                        {
                            string nameWithoutExt = SystemPath.GetFileNameWithoutExtension(file);
                            if (nameWithoutExt.Equals(targetFile, StringComparison.OrdinalIgnoreCase))
                            {
                                fullPath = file;
                                break;
                            }
                        }
                    }
                    if (fullPath == null)
                    {
                        Console.WriteLine($"Файл не найден: {fileName}");
                        return null;
                    }
                }

                string text = File.ReadAllText(fullPath);
                int posIndex = text.IndexOf("/POS", StringComparison.OrdinalIgnoreCase);
                if (posIndex < 0)
                {
                    Console.WriteLine($"Секция /POS не найдена в файле: {fileName}");
                    return null;
                }

                string posSection = text.Substring(posIndex);
                var regex = new Regex(
                    @"P\[\d+\]\s*\{[^}]+X\s*=\s*(?<x>-?\d+(\.\d+)?)\s*mm\s*,\s*" +
                    @"Y\s*=\s*(?<y>-?\d+(\.\d+)?)\s*mm\s*,\s*" +
                    @"Z\s*=\s*(?<z>-?\d+(\.\d+)?)\s*mm[^}]+" +
                    @"R\s*=\s*(?<r>-?\d+(\.\d+)?)\s*deg",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);

                var points = new List<ProgramPoint>();
                foreach (Match match in regex.Matches(posSection))
                {
                    if (match.Success)
                    {
                        points.Add(new ProgramPoint
                        {
                            Point = new Point3D(
                                double.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                                double.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                                double.Parse(match.Groups["z"].Value, CultureInfo.InvariantCulture)),
                            R = double.Parse(match.Groups["r"].Value, CultureInfo.InvariantCulture)
                        });
                    }
                }

                if (points.Count == 0)
                {
                    Console.WriteLine($"Не найдено точек в файле: {fileName}");
                    return null;
                }

                double minX = points.Min(p => p.Point.X);
                double minY = points.Min(p => p.Point.Y);
                double minZ = points.Min(p => p.Point.Z);
                double maxZ = points.Max(p => p.Point.Z);

                var normalized = points.Select(p => new ProgramPoint
                {
                    Point = new Point3D(p.Point.X - minX, p.Point.Y - minY, p.Point.Z),
                    R = p.R
                }).ToList();

                return new LetterTemplate
                {
                    Character = SystemPath.GetFileNameWithoutExtension(fullPath)[0],
                    RelativePoints = normalized,
                    FirstPointRel = new Point(normalized[0].Point.X, normalized[0].Point.Y),
                    LastPointRel = new Point(normalized[normalized.Count - 1].Point.X, normalized[normalized.Count - 1].Point.Y),
                    Width = points.Max(p => p.Point.X) - minX,
                    Height = points.Max(p => p.Point.Y) - minY,
                    BaseZ = minZ,
                    RetractDelta = maxZ - minZ
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при парсинге файла {fileName}: {ex.Message}");
                return null;
            }
        }
    }
}