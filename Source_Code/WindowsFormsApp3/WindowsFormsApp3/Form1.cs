using System;
using System.Drawing;
using System.IO.Pipes;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using System.Runtime.InteropServices;

namespace WindowsFormsApp3
{
    public partial class Form1 : Form
    {
        private bool isDrawing;
        private Point lastPoint;
        private Image backgroundImage;
        private bool isClickThrough;
        private Bitmap drawingBitmap;
        private Graphics drawingGraphics;
        private Thread pipeListenerThread;
        private bool isRunning = true; // Flag to signal the thread to stop

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetWindowLong(IntPtr hWnd, int nIndex);

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;


        private Color drawColor = Color.Black; // Default color
        private int brushSize = 1; // Default size

        public Form1()
        {
            InitializeComponent();
            this.DoubleBuffered = true;

            backgroundImage = Image.FromFile("template_image.png");

            this.Size = new Size(270, 300);
            this.TopMost = true;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.Lime;

            drawingBitmap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
            drawingGraphics = Graphics.FromImage(drawingBitmap);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            var screen = Screen.PrimaryScreen.Bounds;
            this.Location = new Point(screen.Width - this.Width + 7, 0);

            pipeListenerThread = new Thread(ListenForPipeCommands);
            pipeListenerThread.IsBackground = true;
            pipeListenerThread.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (backgroundImage != null)
            {
                e.Graphics.DrawImage(backgroundImage, 0, 0, this.ClientSize.Width, this.ClientSize.Height);
            }

            e.Graphics.DrawImage(drawingBitmap, 0, 0);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && !isClickThrough)
            {
                isDrawing = true;
                lastPoint = e.Location;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (isDrawing && !isClickThrough)
            {
                using (Pen pen = new Pen(drawColor, brushSize))
                {
                    pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                    pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;

                    drawingGraphics.DrawLine(pen, lastPoint, e.Location);
                }
                lastPoint = e.Location;
                this.Invalidate();
            }
        }


        private void ClearDrawing()
        {
            // Reset the drawing area by reloading the background image
            backgroundImage = Image.FromFile("template_image.png");

            // Clear the drawing bitmap to apply the default background again
            drawingGraphics.Clear(Color.Transparent);  // Set the background to transparent

            // Redraw the background image
            this.Invalidate();  // Refresh the form to show the cleared image with the background
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left && !isClickThrough)
            {
                isDrawing = false;
            }
        }

        private void ToggleClickThrough()
        {
            isClickThrough = !isClickThrough;

            if (isClickThrough)
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, (int)(GetWindowLong(this.Handle, GWL_EXSTYLE).ToInt32() | WS_EX_LAYERED | WS_EX_TRANSPARENT));
                this.BackColor = Color.Transparent;
            }
            else
            {
                SetWindowLong(this.Handle, GWL_EXSTYLE, (int)(GetWindowLong(this.Handle, GWL_EXSTYLE).ToInt32() & ~WS_EX_TRANSPARENT));
                this.BackColor = Color.White;
            }
        }

        private void ListenForPipeCommands()
        {
            while (isRunning)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream("CommandPipe", PipeDirection.In))
                    {
                        pipeServer.WaitForConnection();

                        using (var reader = new StreamReader(pipeServer, Encoding.UTF8))
                        {
                            string message = reader.ReadLine();
                            if (message != null)
                            {
                                var parts = message.Split('|');
                                if (parts[0] == "TOGGLE_MODE")
                                {
                                    this.Invoke((MethodInvoker)(() => ToggleClickThrough()));
                                }
                                else if (parts[0] == "SETTINGS")
                                {
                                    // Process color and size settings
                                    foreach (var part in parts)
                                    {
                                        if (part.StartsWith("COLOR:"))
                                        {
                                            string colorName = part.Substring(6);
                                            drawColor = Color.FromName(colorName);
                                        }
                                        else if (part.StartsWith("SIZE:"))
                                        {
                                            if (int.TryParse(part.Substring(5), out int size))
                                            {
                                                brushSize = Math.Max(1, Math.Min(size, 10)); // Ensure valid range
                                            }
                                        }
                                    }

                                    // Apply the new settings immediately
                                    Console.WriteLine($"Updated settings: Color={drawColor.Name}, BrushSize={brushSize}");
                                }
                                else if (message == "CLEAR")
                                {
                                    // Clear the drawing
                                    this.Invoke((MethodInvoker)(() => ClearDrawing()));
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (isRunning) // Log error only if not shutting down
                    {
                        Console.WriteLine($"Error in pipe communication: {ex.Message}");
                    }
                }
            }
        }


        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            isRunning = false; // Signal the thread to stop
            if (pipeListenerThread != null && pipeListenerThread.IsAlive)
            {
                pipeListenerThread.Join(); // Wait for the thread to terminate
            }
            drawingGraphics.Dispose();
            drawingBitmap.Dispose();
            backgroundImage.Dispose();
        }
    }
}
