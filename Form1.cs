using System;
using System.Drawing; // Bitmap için gerekli
using System.Windows.Forms;
using OpenCvSharp; // Ana OpenCvSharp kütüphanesi
using System.Drawing.Imaging; // Mat'ı Bitmap'e çevirmek için gerekli
using System.Linq;

namespace OpenCv
{
    public partial class Form1 : Form
    {
        // OpenCV bileşenlerini tüm metotlardan erişilebilmesi için sınıf seviyesinde tanımlıyoruz
        VideoCapture capture;       // Web kamerasını temsil eder
        Mat frame;                  // Kameradan alınan her bir kareyi tutar
        CascadeClassifier handCascade; // El tanıma modelini tutar
        CascadeClassifier faceCascade;
        OpenCvSharp.Rect? lastHand = null;
        int stableCount = 0, missingCount = 0;
        const int REQ_STABLE = 3, HOLD_FRAMES = 10;

        public Form1()
        {
            InitializeComponent();
            this.Load += Form1_Load;
            this.FormClosing += Form1_FormClosing;
            btnStartStop.Click += btnStartStop_Click;
            timer1.Tick += timer1_Tick;

            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
        }

        // Form yüklendiğinde (program ilk açıldığında) çalışır
        private void Form1_Load(object sender, EventArgs e)
        {
            // El modeli
            string handPath = "haarcascade_hand.xml"; // Properties: Content + Copy if newer
            handCascade = new CascadeClassifier();
            if (!handCascade.Load(handPath))
            {
                MessageBox.Show("Hata: 'hand.xml' yüklenemedi. Dosyayı çıktı klasörüne kopyalayın.",
                    "Model Yükleme Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                btnStartStop.Enabled = false;
                return;
            }

            // Yüz modeli (yüzü dışlamak için) - bulunamazsa uyarı verip yüz dışlamayı kapatır
            string facePath = "haarcascade_frontalface_default.xml"; // Properties: Content + Copy if newer
            faceCascade = new CascadeClassifier();
            if (!faceCascade.Load(facePath))
            {
                MessageBox.Show("Uyarı: 'haarcascade_frontalface_default.xml' yüklenemedi. Yüz dışlama devre dışı.",
                    "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                faceCascade = null;
            }

            frame = new Mat();
        }

        // Başlat/Durdur butonuna tıklandığında çalışır
        private void btnStartStop_Click(object sender, EventArgs e)
        {
            if (timer1.Enabled) // Timer çalışıyorsa (Kamera açıksa)
            {
                // Kamerayı Durdur
                timer1.Stop();
                capture.Release(); // Kamerayı serbest bırak (ÇOK ÖNEMLİ)
                btnStartStop.Text = "Başlat";
            }
            else // Timer çalışmıyorsa (Kamera kapalıysa)
            {
                // Kamerayı Başlat
                capture = new VideoCapture(0); // 0, varsayılan web kamerasıdır
                if (!capture.IsOpened())
                {
                    MessageBox.Show("Kamera açılamadı!", "Kamera Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                timer1.Start(); // Timer'ı (Görüntü işleme döngüsünü) başlat
                btnStartStop.Text = "Durdur";
            }
        }

        // Timer her "Interval" (33ms) süresinde bir tetiklenir
        private void timer1_Tick(object sender, EventArgs e)
        {
            capture.Read(frame);
            if (frame.Empty())
            {
                timer1.Stop();
                return;
            }

            Mat gray = new Mat();
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
            Cv2.EqualizeHist(gray, gray);
            Cv2.GaussianBlur(gray, gray, new OpenCvSharp.Size(5, 5), 0);

            // Yüzleri bul (dışlamak için)
            var faces = faceCascade?.DetectMultiScale(
                image: gray, scaleFactor: 1.1, minNeighbors: 4,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(100, 100)
            ) ?? Array.Empty<OpenCvSharp.Rect>();

            // En büyük el adayını bul
            var cand = handCascade.DetectMultiScale(
                image: gray, scaleFactor: 1.02, minNeighbors: 3,
                flags: HaarDetectionTypes.FindBiggestObject | HaarDetectionTypes.ScaleImage,
                minSize: new OpenCvSharp.Size(100, 100)
            );

            OpenCvSharp.Rect? detected = null;
            if (cand.Length > 0)
            {
                var r = cand[0];
                bool overlapsFace = faces.Any(f =>
                {
                    var inter = OpenCvSharp.Rect.Intersect(r, f);
                    double iou = (inter.Width * inter.Height) /
                                 (double)(r.Width * r.Height + f.Width * f.Height - inter.Width * inter.Height + 1e-5);
                    bool centerInFace = f.Contains(new OpenCvSharp.Point(r.X + r.Width / 2, r.Y + r.Height / 2));
                    return iou > 0.20 || centerInFace;
                });

                double ar = r.Width / (double)r.Height; // oran kontrolü
                if (!overlapsFace && ar >= 0.6 && ar <= 1.6)
                    detected = r;
            }

            // Stabilizasyon
            if (detected.HasValue)
            {
                if (lastHand.HasValue)
                {
                    var prev = lastHand.Value;
                    int x = (int)(0.7 * prev.X + 0.3 * detected.Value.X);
                    int y = (int)(0.7 * prev.Y + 0.3 * detected.Value.Y);
                    int w = (int)(0.7 * prev.Width + 0.3 * detected.Value.Width);
                    int h = (int)(0.7 * prev.Height + 0.3 * detected.Value.Height);
                    lastHand = new OpenCvSharp.Rect(x, y, w, h);
                }
                else
                {
                    lastHand = detected;
                }
                stableCount++;
                missingCount = 0;
            }
            else
            {
                missingCount++;
                if (missingCount > HOLD_FRAMES)
                {
                    lastHand = null;
                    stableCount = 0;
                }
            }

            // Çizim (padding ile büyüt)
            if (lastHand.HasValue && stableCount >= REQ_STABLE)
            {
                var r = lastHand.Value;
                int padX = (int)(r.Width * 0.6);
                int padY = (int)(r.Height * 0.6);
                int x = Math.Max(0, r.X - padX);
                int y = Math.Max(0, r.Y - padY);
                int w = Math.Min(frame.Width - x, r.Width + 2 * padX);
                int h = Math.Min(frame.Height - y, r.Height + 2 * padY);
                var big = new OpenCvSharp.Rect(x, y, w, h);

                Cv2.Rectangle(frame, big, new Scalar(0, 255, 0), 2);
                Cv2.PutText(frame, "El", new OpenCvSharp.Point(big.X, big.Y - 10),
                    HersheyFonts.HersheyComplexSmall, 1, new Scalar(0, 255, 0));
            }

            var bitmap = new System.Drawing.Bitmap(
                frame.Width, frame.Height, (int)frame.Step(),
                PixelFormat.Format24bppRgb, frame.Data);

            if (pictureBox1.Image != null) pictureBox1.Image.Dispose();
            pictureBox1.Image = bitmap;
        }

        // Form kapatılırken kameranın serbest bırakıldığından emin ol
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (capture != null && capture.IsOpened())
            {
                capture.Release();
            }
        }
    }
}