using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Tao.OpenGl;
using System.IO;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using System.Reflection.Emit;
using SharpGL;
using System.Security.Cryptography;
using SharpGL.SceneGraph.Cameras;
using Tao.Platform.Windows;
using Tao.FreeGlut;

namespace FanucProgrammer
{
    





    public partial class Simulate : Form
    {
        private List<Point3D> points = new List<Point3D>();

        // Границы рабочей зоны (соответствуют размерам плоскости в симуляции)
        private const float WORK_ZONE_MIN_X = -100f;
        private const float WORK_ZONE_MAX_X = 100f;
        private const float WORK_ZONE_MIN_Y = -100f;
        private const float WORK_ZONE_MAX_Y = 100f;
        private const float WORK_ZONE_MIN_Z = 0f;
        private const float WORK_ZONE_MAX_Z = 100f;





        private float cameraX = 0f;
        private float cameraY = 0f;
        private float cameraZ = 500f;
        private float moveSpeed = 1.1f;




        double angleX = 0f, angleY = 0f;
        //private float planeWidth = 5.0f; // Ширина плоскости
        //private float planeHeight = 5.0f; // Высота плоскости
        public Simulate()
        {
            InitializeComponent();
            simpleOpenGlControl1.Paint += simpleOpenGlControl1_Paint;
            this.KeyDown += simpleOpenGlControl1_KeyDown;
            simpleOpenGlControl1.Focus();

            // Инициализация OpenGL
            simpleOpenGlControl1.InitializeContexts();
            int width = simpleOpenGlControl1.Width;
            int height = simpleOpenGlControl1.Height;
            Gl.glViewport(0, 0, width, height);
            Gl.glEnable(Gl.GL_DEPTH_TEST); // Включение теста глубины
            Gl.glClearColor(0.3f, 0.3f, 0.4f, 1.0f);
            Gl.glMatrixMode(Gl.GL_PROJECTION);
            Gl.glLoadIdentity();
            Glu.gluPerspective(45.0f, (double)width / (double)height, 0.1f, 1000.0f);
            Gl.glMatrixMode(Gl.GL_MODELVIEW);
            Gl.glLoadIdentity();

            InitializeTrackBars();
        }

        private void simpleOpenGlControl1_Paint(object sender, PaintEventArgs e)
        {
            Gl.glClear(Gl.GL_COLOR_BUFFER_BIT | Gl.GL_DEPTH_BUFFER_BIT);
            Gl.glLoadIdentity();

            // Установка камеры
            Glu.gluLookAt(cameraX, cameraY, cameraZ, 0, 0, 0, 0, 1, 0);

            // Применение поворотов
            Gl.glRotated(angleX, 1.0f, 0.0f, 0.0f);
            Gl.glRotated(angleY, 0.0f, 1.0f, 0.0f);

            DrawZeroPlane();
            DrawAxes();
            DrawZeroCoordinateSystem();
            DrawWorkZone();

            // Рисуем линии между точками
            DrawConnectingLines();

            // Рисуем точки с подписями
            for (int i = 0; i < points.Count; i++)
            {
                DrawPointWithLabel(points[i], i + 1);
            }

            Gl.glFlush();
            simpleOpenGlControl1.SwapBuffers();
        }

        private void DrawZeroPlane()
        {
            float gridSize = 100.0f;
            float gridStep = 100.0f;

            Gl.glEnable(Gl.GL_BLEND);
            Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);

            // Рисование плоскости
            Gl.glColor4f(0.5f, 0.5f, 0.8f, 0.3f);
            Gl.glBegin(Gl.GL_QUADS);
            Gl.glVertex3f(-gridSize, -gridSize, 0);
            Gl.glVertex3f(gridSize, -gridSize, 0);
            Gl.glVertex3f(gridSize, gridSize, 0);
            Gl.glVertex3f(-gridSize, gridSize, 0);
            Gl.glEnd();

            // Рисование сетки
            Gl.glColor4f(0.7f, 0.7f, 0.7f, 0.5f);
            Gl.glBegin(Gl.GL_LINES);
            for (float i = -gridSize; i <= gridSize; i += gridStep)
            {
                Gl.glVertex3f(-gridSize, i, 0);
                Gl.glVertex3f(gridSize, i, 0);
                Gl.glVertex3f(i, -gridSize, 0);
                Gl.glVertex3f(i, gridSize, 0);
            }
            Gl.glEnd();
            Gl.glDisable(Gl.GL_BLEND);
        }

        private void DrawPoint(Point3D p, int index)
        {
            Gl.glPointSize(10);
            
            // Проверяем, находится ли точка в рабочей зоне
            if (IsPointInWorkZone(p))
            {
                Gl.glColor3f(1, 1, 0); // Желтый цвет для точек в рабочей зоне
            }
            else
            {
                Gl.glColor3f(1, 0, 0); // Красный цвет для точек вне рабочей зоны
            }
            
            Gl.glBegin(Gl.GL_POINTS);
            Gl.glVertex3f(p.X, p.Y, p.Z);
            Gl.glEnd();

            // Рисуем подпись точки
            DrawPointLabel(p, index);
        }

        private void DrawPointWithLabel(Point3D p, int index)
        {
            // Рисуем точку
            Gl.glPointSize(10);
            
            // Проверяем, находится ли точка в рабочей зоне
            if (IsPointInWorkZone(p))
            {
                Gl.glColor3f(1, 1, 0); // Желтый цвет для точек в рабочей зоне
            }
            else
            {
                Gl.glColor3f(1, 0, 0); // Красный цвет для точек вне рабочей зоны
            }
            
            Gl.glBegin(Gl.GL_POINTS);
            Gl.glVertex3f(p.X, p.Y, p.Z);
            Gl.glEnd();

            // Рисуем подпись точки как часть OpenGL сцены
            DrawOpenGLText(p, index);
        }

        private void DrawPointOnly(Point3D p, int index)
        {
            Gl.glPointSize(10);
            
            // Проверяем, находится ли точка в рабочей зоне
            if (IsPointInWorkZone(p))
            {
                Gl.glColor3f(1, 1, 0); // Желтый цвет для точек в рабочей зоне
            }
            else
            {
                Gl.glColor3f(1, 0, 0); // Красный цвет для точек вне рабочей зоны
            }
            
            Gl.glBegin(Gl.GL_POINTS);
            Gl.glVertex3f(p.X, p.Y, p.Z);
            Gl.glEnd();
        }

        private bool IsPointInWorkZone(Point3D point)
        {
            return point.X >= WORK_ZONE_MIN_X && point.X <= WORK_ZONE_MAX_X &&
                   point.Y >= WORK_ZONE_MIN_Y && point.Y <= WORK_ZONE_MAX_Y &&
                   point.Z >= WORK_ZONE_MIN_Z && point.Z <= WORK_ZONE_MAX_Z;
        }

        private void DrawPointLabel(Point3D p, int index)
        {
            // Преобразуем 3D координаты в 2D экранные координаты
            double[] modelView = new double[16];
            double[] projection = new double[16];
            int[] viewport = new int[4];
            
            Gl.glGetDoublev(Gl.GL_MODELVIEW_MATRIX, modelView);
            Gl.glGetDoublev(Gl.GL_PROJECTION_MATRIX, projection);
            Gl.glGetIntegerv(Gl.GL_VIEWPORT, viewport);

            // Преобразуем 3D координаты в экранные
            double screenX, screenY, screenZ;
            Glu.gluProject(p.X + 10, p.Y + 10, p.Z, modelView, projection, viewport, 
                          out screenX, out screenY, out screenZ);

            // Проверяем, что точка находится в пределах экрана
            if (screenZ > 0 && screenZ < 1 && 
                screenX >= 0 && screenX <= simpleOpenGlControl1.Width &&
                screenY >= 0 && screenY <= simpleOpenGlControl1.Height)
            {
                // Рисуем текст поверх OpenGL с помощью GDI+
                using (Graphics g = simpleOpenGlControl1.CreateGraphics())
                {
                    using (Font font = new Font("Arial", 16, FontStyle.Bold))
                    using (Brush brush = new SolidBrush(Color.White))
                    using (Brush outlineBrush = new SolidBrush(Color.Black))
                    {
                        string label = index.ToString();
                        PointF textPoint = new PointF((float)screenX, (float)(simpleOpenGlControl1.Height - screenY));
                        
                        // Рисуем контур текста для лучшей читаемости
                        for (int x = -1; x <= 1; x++)
                        {
                            for (int y = -1; y <= 1; y++)
                            {
                                if (x != 0 || y != 0)
                                {
                                    g.DrawString(label, font, outlineBrush, 
                                               textPoint.X + x, textPoint.Y + y);
                                }
                            }
                        }
                        
                        // Рисуем основной текст
                        g.DrawString(label, font, brush, textPoint);
                    }
                }
            }
        }

        private void DrawOpenGLText(Point3D p, int index)
        {
            // Устанавливаем цвет для текста
            Gl.glColor3f(1.0f, 1.0f, 1.0f); // Белый цвет
            
            // Размер текста
            float textSize = 3.0f;
            float offsetX = 8.0f;
            float offsetY = 8.0f;
            
            // Рисуем цифру как простые линии
            string number = index.ToString();
            float currentX = p.X + offsetX;
            
            foreach (char digit in number)
            {
                DrawDigit(currentX, p.Y + offsetY, p.Z, digit, textSize);
                currentX += textSize * 4; // Смещение для следующей цифры
            }
        }

        private void DrawDigit(float x, float y, float z, char digit, float size)
        {
            Gl.glLineWidth(2.0f);
            Gl.glBegin(Gl.GL_LINES);
            
            switch (digit)
            {
                case '0':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x, y - size, z);
                    // Левая линия
                    Gl.glVertex3f(x, y - size, z);
                    Gl.glVertex3f(x, y + size, z);
                    break;
                    
                case '1':
                    // Вертикальная линия
                    Gl.glVertex3f(x + size, y + size, z);
                    Gl.glVertex3f(x + size, y - size, z);
                    // Верхняя наклонная линия
                    Gl.glVertex3f(x, y + size * 0.5f, z);
                    Gl.glVertex3f(x + size, y + size, z);
                    break;
                    
                case '2':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая верхняя линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    // Средняя линия
                    Gl.glVertex3f(x + size * 2, y, z);
                    Gl.glVertex3f(x, y, z);
                    // Левая нижняя линия
                    Gl.glVertex3f(x, y, z);
                    Gl.glVertex3f(x, y - size, z);
                    // Нижняя линия
                    Gl.glVertex3f(x, y - size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    break;
                    
                case '3':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Средняя линия
                    Gl.glVertex3f(x + size * 2, y, z);
                    Gl.glVertex3f(x + size, y, z);
                    // Нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x + size, y - size, z);
                    break;
                    
                case '4':
                    // Левая верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x, y, z);
                    // Средняя линия
                    Gl.glVertex3f(x, y, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    break;
                    
                case '5':
                    // Верхняя линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x, y + size, z);
                    // Левая верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x, y, z);
                    // Средняя линия
                    Gl.glVertex3f(x, y, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    // Правая нижняя линия
                    Gl.glVertex3f(x + size * 2, y, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x, y - size, z);
                    break;
                    
                case '6':
                    // Верхняя линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x, y + size, z);
                    // Левая линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x, y - size, z);
                    // Нижняя линия
                    Gl.glVertex3f(x, y - size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Правая нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    // Средняя линия
                    Gl.glVertex3f(x + size * 2, y, z);
                    Gl.glVertex3f(x, y, z);
                    break;
                    
                case '7':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    break;
                    
                case '8':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x, y - size, z);
                    // Левая линия
                    Gl.glVertex3f(x, y - size, z);
                    Gl.glVertex3f(x, y + size, z);
                    // Средняя линия
                    Gl.glVertex3f(x, y, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    break;
                    
                case '9':
                    // Верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    // Правая линия
                    Gl.glVertex3f(x + size * 2, y + size, z);
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    // Левая верхняя линия
                    Gl.glVertex3f(x, y + size, z);
                    Gl.glVertex3f(x, y, z);
                    // Средняя линия
                    Gl.glVertex3f(x, y, z);
                    Gl.glVertex3f(x + size * 2, y, z);
                    // Нижняя линия
                    Gl.glVertex3f(x + size * 2, y - size, z);
                    Gl.glVertex3f(x, y - size, z);
                    break;
            }
            
            Gl.glEnd();
        }


        private void DrawConnectingLines()
        {
            if (points.Count < 2) return; // Нужно минимум 2 точки для соединения

            Gl.glLineWidth(2.0f);
            Gl.glColor3f(0.0f, 1.0f, 1.0f); // Голубой цвет для линий
            Gl.glBegin(Gl.GL_LINE_STRIP);

            // Соединяем все точки последовательно
            for (int i = 0; i < points.Count; i++)
            {
                Gl.glVertex3f(points[i].X, points[i].Y, points[i].Z);
            }

            Gl.glEnd();
        }

        private void DrawWorkZone()
        {
            // Рисуем границы рабочей зоны как полупрозрачный параллелепипед
            Gl.glEnable(Gl.GL_BLEND);
            Gl.glBlendFunc(Gl.GL_SRC_ALPHA, Gl.GL_ONE_MINUS_SRC_ALPHA);
            
            // Полупрозрачные грани параллелепипеда
            Gl.glColor4f(0.0f, 1.0f, 0.0f, 0.15f); // Зеленый с прозрачностью
            
            // Рисуем грани параллелепипеда
            Gl.glBegin(Gl.GL_QUADS);
            
            // Передняя грань (Z = MAX_Z)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            
            // Задняя грань (Z = MIN_Z = 0) - совпадает с плоскостью
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            
            // Верхняя грань (Y = MAX_Y)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            
            // Нижняя грань (Y = MIN_Y)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            
            // Левая грань (X = MIN_X)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            
            // Правая грань (X = MAX_X)
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            
            Gl.glEnd();
            
            // Рисуем контуры параллелепипеда
            Gl.glColor3f(0.0f, 0.8f, 0.0f); // Темно-зеленый цвет для контуров
            Gl.glLineWidth(2.0f);
            Gl.glBegin(Gl.GL_LINES);
            
            // Контуры параллелепипеда
            // Нижняя грань (Z = 0)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            
            // Верхняя грань (Z = MAX_Z)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            
            // Вертикальные ребра (соединяют нижнюю и верхнюю грани)
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MIN_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MAX_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MIN_Z);
            Gl.glVertex3f(WORK_ZONE_MIN_X, WORK_ZONE_MAX_Y, WORK_ZONE_MAX_Z);
            
            Gl.glEnd();
            Gl.glDisable(Gl.GL_BLEND);
        }

        private void DrawZeroCoordinateSystem()
        {
            Gl.glBegin(Gl.GL_LINES);
            // X-axis
            Gl.glColor3f(1.0f, 0.0f, 0.0f);
            Gl.glVertex3d(0, 0, 0);
            Gl.glVertex3d(10, 0, 0);
            // Y-axis
            Gl.glColor3f(0.0f, 1.0f, 0.0f);
            Gl.glVertex3d(0, 0, 0);
            Gl.glVertex3d(0, 10, 0);
            // Z-axis
            Gl.glColor3f(0.0f, 0.0f, 1.0f);
            Gl.glVertex3d(0, 0, 0);
            Gl.glVertex3d(0, 0, 10);
            Gl.glEnd();
        }

        private void DrawAxes()
        {
            Gl.glBegin(Gl.GL_LINES); // Начинаем рисовать линии
            // Рисуем ось X (красная)
            Gl.glColor3f(1.0f, 0.0f, 0.0f); // Устанавливаем цвет красный
            Gl.glVertex3f(0, 0, 0); // Левый конец оси X
            Gl.glVertex3f(10, 0, 0); // Правый конец оси X
            // Рисуем ось Y (зеленая)
            Gl.glColor3f(0.0f, 1.0f, 0.0f); // Устанавливаем цвет зеленый
            Gl.glVertex3f(0, 0, 0); // Нижний конец оси Y
            Gl.glVertex3f(0, 10, 0); // Верхний конец оси Y
            // Рисуем ось Z (синяя)
            Gl.glColor3f(0.0f, 0.0f, 1.0f); // Устанавливаем цвет синий
            Gl.glVertex3f(0, 0, 0); // Левый конец оси Z
            Gl.glVertex3f(0, 0, 10); // Правый конец оси Z
            Gl.glEnd(); // Завершаем рисование линий
            //// Грань 1
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor3ub(255, 0, 255);
            //Gl.glVertex3d(1, 1, -1);
            //Gl.glVertex3d(1, -1, -1);
            //Gl.glVertex3d(-1, -1, -1);
            //Gl.glVertex3d(-1, 1, -1);
            //Gl.glEnd();

            //// Грань 2
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor3ub(0, 255, 255);
            //Gl.glVertex3d(-1, -1, -1);
            //Gl.glVertex3d(1, -1, -1);
            //Gl.glVertex3d(1, -1, 1);
            //Gl.glVertex3d(-1, -1, 1);
            //Gl.glEnd();

            //// Грань 3
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor3ub(255, 255, 0);
            //Gl.glVertex3d(-1, 1, -1);
            //Gl.glVertex3d(-1, -1, -1);
            //Gl.glVertex3d(-1, -1, 1);
            //Gl.glVertex3d(-1, 1, 1);
            //Gl.glEnd();

            //// Грань 4
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor3ub(0, 0, 255);
            //Gl.glVertex3d(1, 1, 1);
            //Gl.glVertex3d(1, -1, 1);
            //Gl.glVertex3d(1, -1, -1);
            //Gl.glVertex3d(1, 1, -1);
            //Gl.glEnd();

            //// Грань 5
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor3ub(0, 255, 0);
            //Gl.glVertex3d(-1, 1, -1);
            //Gl.glVertex3d(-1, 1, 1);
            //Gl.glVertex3d(1, 1, 1);
            //Gl.glVertex3d(1, 1, -1);
            //Gl.glEnd();

            //// Грань 6
            //Gl.glBegin(Gl.GL_LINE_LOOP);
            //Gl.glColor4d(255, 0, 0, 100);
            //Gl.glVertex3d(-1, 1, 1);
            //Gl.glVertex3d(-1, -1, 1);
            //Gl.glVertex3d(1, -1, 1);
            //Gl.glVertex3d(1, 1, 1);
            //Gl.glEnd();
        }
        


        
        private void button1_Click(object sender, EventArgs e)
        {
            //xmove = Convert.ToDouble(textBox1.Text);
            //ymove = Convert.ToDouble(textBox2.Text);
            //zmove = Convert.ToDouble(textBox3.Text);
            if (float.TryParse(textBox1.Text, out float x) &&
                float.TryParse(textBox2.Text, out float y) &&
                float.TryParse(textBox3.Text, out float z))

            //    (float.TryParse(textBox1.Text, out cameraX) &&
            //float.TryParse(textBox2.Text, out cameraY) &&
            //float.TryParse(textBox3.Text, out cameraZ))


            {
                points.Add(new Point3D(x, y, z));
                simpleOpenGlControl1.Invalidate();
            }
            else { MessageBox.Show("Введите корректные числа для X, Y, Z."); }


           




        }



        public class Point3D
        {
            public float X, Y, Z;
            public Point3D(float x, float y, float z)
            {
                X = x; Y = y; Z = z;
            }
        }







        private void button2_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "List files (*.ls)|*.ls|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    LoadListBoxFromFile(filePath);
                }
            }

        }
        private void LoadListBoxFromFile(string filePath)
        {
            listBox1.Items.Clear(); // Очистить текущие элементы списка
            try
            {
                using (StreamReader reader = new StreamReader(filePath))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        listBox1.Items.Add(line); // Добавить строку в ListBox
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке файла: {ex.Message}");
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string result = "";

            foreach (string item in listBox1.Items)
            {
                // Находим индекс слова "LINE_COUNT"
                int startIndex = item.IndexOf("LINE_COUNT\t=");
                // Находим индекс символа ";"
                int endIndex = item.IndexOf(";");

                // Проверяем, что оба индекса найдены и startIndex меньше endIndex
                if (startIndex != -1 && endIndex != -1 && startIndex < endIndex)
                {
                    // Извлекаем подстроку между "LINE_COUNT" и ";"
                    result = item.Substring(startIndex + "LINE_COUNT\t=".Length, endIndex - (startIndex + "LINE_COUNT\t=".Length)).Trim();
                    MessageBox.Show(result, "Общее число строк в программе:");
                }
            }


            int countlines = 0;
            // Очистка второго ListBox перед добавлением новых значений
            listBox2.Items.Clear();

            foreach (string item in listBox1.Items)
            {
                // Находим индекс слова "X="
                int index = item.IndexOf("X =   ");
                if (index != -1)
                {
                    countlines++;
                    // Извлекаем значение после "X="
                    string value = item.Substring(index).Trim(); // +2, чтобы пропустить "X="
                    listBox2.Items.Add(value);
                    
                }
                
            }
            MessageBox.Show(Convert.ToString(countlines), "Число выписанных строк для эмуляции: ");

        }
        
        private void button4_Click(object sender, EventArgs e)
        {
            
           
        }

        private string ExtractValue(string item)
        {
            // Ищем позицию "X =" и "mm"
            int startIndex = item.IndexOf("X =   ") + 6; // +3 чтобы пропустить "X ="
            int endIndex = item.IndexOf("  mm", startIndex); // Ищем "mm" начиная с индекса после "X ="

            // Извлекаем подстроку
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return item.Substring(startIndex, endIndex - startIndex).Trim(); // Удаляем возможные пробелы
            }

            return string.Empty; // Если не найдено, возвращаем пустую строку
        }
        private string ExtractValue2(string item)
        {
            // Ищем позицию "X =" и "mm"
            int startIndex = item.IndexOf("Y =   ") + 6; // +3 чтобы пропустить "X ="
            int endIndex = item.IndexOf("  mm", startIndex); // Ищем "mm" начиная с индекса после "X ="

            // Извлекаем подстроку
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return item.Substring(startIndex, endIndex - startIndex).Trim(); // Удаляем возможные пробелы
            }

            return string.Empty; // Если не найдено, возвращаем пустую строку
        }
        private string ExtractValue3(string item)
        {
            // Ищем позицию "X =" и "mm"
            int startIndex = item.IndexOf("Z =   ") + 6; // +3 чтобы пропустить "X ="
            int endIndex = item.IndexOf("  mm", startIndex); // Ищем "mm" начиная с индекса после "X ="

            // Извлекаем подстроку
            if (startIndex >= 0 && endIndex > startIndex)
            {
                return item.Substring(startIndex, endIndex - startIndex).Trim(); // Удаляем возможные пробелы
            }

            return string.Empty; // Если не найдено, возвращаем пустую строку
        }

        private int currentIndex = -1;

        private void button1_MouseDown(object sender, MouseEventArgs e)
        {

        }

        private void button1_MouseDown_1(object sender, MouseEventArgs e)
        {
            if (listBox2.Items.Count == 0) return; // Если список пуст, ничего не делаем

            // Переход к следующему элементу
            int nextIndex1 = (listBox2.SelectedIndex + 1) % listBox2.Items.Count; // Можно использовать остаток от деления
            listBox2.SelectedIndex = nextIndex1; // Обновление выбранного элемента

            // Извлечение значения между "X =" и "mm"
            string selectedItem1 = listBox2.SelectedItem.ToString();
            string extractedValue1 = ExtractValue(selectedItem1);
            extractedValue1 = extractedValue1.Replace('.', ',');


            // Заполнение TextBox
            textBox1.Text = extractedValue1;



            string selectedItem2 = listBox2.SelectedItem.ToString();
            string extractedValue2 = ExtractValue2(selectedItem2);
            extractedValue2 = extractedValue2.Replace('.', ',');
            textBox2.Text = extractedValue2;


            string selectedItem3 = listBox2.SelectedItem.ToString();
            string extractedValue3 = ExtractValue3(selectedItem3);
            extractedValue3 = extractedValue3.Replace('.', ',');
            textBox3.Text = extractedValue3;
        }
        private void InitializeTrackBars()
        {
            trackBar1.Minimum = 0; // Минимальное значение
            trackBar1.Maximum = 360; // Максимальное значение
           trackBar1.TickFrequency = 10; // Шаг при перемещении ползунка
            trackBar1.ValueChanged += trackBar1_ValueChanged; // Подписываемся на событие
           // trackBar1.Dock = DockStyle.Top; // Устанавливаем dock для расположения
            //this.Controls.Add(trackBar1);
            trackBar2.Minimum = 0; // Минимальное значение
            trackBar2.Maximum = 360; // Максимальное значение
            trackBar2.TickFrequency = 10; // Шаг при перемещении ползунка
            trackBar2.ValueChanged += trackBar2_ValueChanged; // Подписываемся на событие
        }

        private void trackBar2_ValueChanged(object sender, EventArgs e)
        {
            angleY = trackBar2.Value; // Установка угла вращения вокруг оси X
            simpleOpenGlControl1.Invalidate(); // Перерисовать контроль
        }

        private void tabPage2_Click(object sender, EventArgs e)
        {

        }
        private PointF lastMousePosition = PointF.Empty;

        private void trackBar1_Scroll(object sender, EventArgs e)
        {

        }

        private void simpleOpenGlControl1_Load(object sender, EventArgs e)
        {

        }

        private void simpleOpenGlControl1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.W: // Движение вперед
                    cameraZ -= moveSpeed;
                    break;
                case Keys.S: // Движение назад
                    cameraZ += moveSpeed;
                    break;
                case Keys.A: // Движение влево
                    cameraX -= moveSpeed;
                    break;
                case Keys.D: // Движение вправо
                    cameraX += moveSpeed;
                    break;
                case Keys.Up: // Поворот вверх
                    angleX -= 5.0f;
                    break;
                case Keys.Down: // Поворот вниз
                    angleX += 5.0f;
                    break;
                case Keys.Left: // Поворот влево
                    angleY -= 5.0f;
                    break;
                case Keys.Right: // Поворот вправо
                    angleY += 5.0f;
                    break;
            }

            simpleOpenGlControl1.Invalidate(); // Перерисовать сцену
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            // Обработка клавиш
            simpleOpenGlControl1_KeyDown(this, new KeyEventArgs(keyData));
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void trackBar1_ValueChanged(object sender, EventArgs e)
        {
            angleX = trackBar1.Value; // Установка угла вращения вокруг оси X
            simpleOpenGlControl1.Invalidate(); // Перерисовать контроль
        }

        

        private void Simulate_Load(object sender, EventArgs e)
        {
            textBox1.Text = "0";
            double a = Convert.ToDouble(textBox1.Text);
            textBox2.Text = "0";
            double b = Convert.ToDouble(textBox2.Text);
            textBox3.Text = "0";
            double tb3 = Convert.ToDouble(textBox3.Text);
            
        }

        private void button6_Click(object sender, EventArgs e)
        {
            
            simpleOpenGlControl1.Invalidate(); // Перерисовать контроль
        }

        double xmove, ymove, zmove = 0;

    }
}
