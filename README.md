## OpenCV El Algılama (Yumruk → Algıla, Açık El → Durdur)

Bu WinForms projesi, web kamerasından görüntü alır ve elinizi yumruk yaptığınızda algılar, elinizi açtığınızda algılamayı keser. Algılama alanı yeşil bir kutu ve "El" etiketiyle gösterilir.

### Özellikler
- **Kamera akışı**: `VideoCapture(0)` ile varsayılan kameradan gerçek zamanlı görüntü.
- **El tespiti (Haar cascade)**: `hand.xml` ile `DetectMultiScale`.
- **Yüz dışlama**: `haarcascade_frontalface_default.xml` ile yüz kutusu ile çakışan el adaylarını eler.
- **Stabilizasyon**: Ani kaymaları azaltmak için kutu konumu EMA ile yumuşatılır, kısa kayıplarda kutu bir süre tutulur.
- **Davranış**: 
  - **Yumruk** (parmaklar kapalı) → tespit daha kararlı; kısa sürede kutu görünür.
  - **Açık el** (parmaklar ayrık) → tespit zayıflar/kaybolur; kısa tutma süresi sonunda kutu kaybolur.

### Demo
- `media/` klasörüne bir GIF/MP4 ekleyip buraya koyabilirsiniz: `media/demo.gif`.

### Gereksinimler
- Windows + Visual Studio
- .NET Framework hedefi: `net48` (proje böyle yapılandırıldı)
- NuGet paketleri (otomatik restore):
  - `OpenCvSharp4` 4.11.0
  - `OpenCvSharp4.runtime.win`
  - `System.Buffers`, `System.Memory`, `System.Numerics.Vectors`, `System.Runtime.CompilerServices.Unsafe`

### Kurulum
1. Repostoyu klonlayın.
2. `OpenCv.sln` dosyasını Visual Studio ile açın.
3. NuGet paketleri otomatik indirilecektir.
4. Aşağıdaki iki XML dosyasını projeye ekleyin (varsa doğrulayın) ve özelliklerini ayarlayın:
   - `hand.xml` (el cascade)
   - `haarcascade_frontalface_default.xml` (yüz cascade)
   - Her ikisi için:
     - **Build Action**: Content
     - **Copy to Output Directory**: Copy if newer

### Çalıştırma
- `OpenCv` projesini Başlatın.
- Form açılınca “Başlat” butonuna tıklayın.
- Kameraya elinizi gösterin:
  - Yumruk yaptığınızda yeşil kutu ve “El” etiketi görünür.
  - Elinizi açtığınızda (ayrık parmaklar), kısa bir gecikmeden sonra kutu kaybolur.

### Nasıl Çalışır (Kısa)
- `timer1_Tick` içinde akış:
  - BGR → gri, `EqualizeHist`, ardından **GaussianBlur** ile gürültü azaltılır.
  - `hand.xml` ile el adayları bulunur:  
    `FindBiggestObject`, `scaleFactor≈1.02`, `minNeighbors≈3`, `minSize≈100x100`.
  - `haarcascade_frontalface_default.xml` ile bulunan yüz(ler) tespit edilir ve el adayları yüz ile çakışıyorsa elenir.
  - Zaman içinde **EMA** ile kutu yumuşatılır ve kısa kayıplarda `HOLD_FRAMES` kadar tutulur.
  - Çizimde, kutu bir miktar **padding** ile büyütülerek tam ele yakın görünüm sağlanır.

### Parametre Ayarları
- `minNeighbors`: 3–6 arası. Büyük değer = daha seçici.
- `minSize`: 80–160 px. Çok küçük değer yanlış pozitif, çok büyük değer küçük elleri kaçırır.
- `HOLD_FRAMES`: 10–20 arası. Kopmaları ne kadar tolere edeceğinizi belirler.
- `REQ_STABLE`: 2–4 arası. Kutu görünmeden önce gereken kararlı kare sayısı.

### Proje Yapısı (özet)
- `OpenCv/Form1.cs`: Kamera/algılama/stabilizasyon/çizim mantığı.
- `OpenCv/Form1.Designer.cs`: UI bileşenleri (`pictureBox1`, `btnStartStop`, `timer1`).
- `hand.xml`: El Haar cascade modeli.
- `haarcascade_frontalface_default.xml`: Yüz Haar cascade modeli.

### Sık Sorunlar ve Çözümler
- Kamera açılmıyor: Windows Gizlilik ayarlarından uygulamalara kamera izni verin; başka uygulama kamerayı kullanmıyor olmalı.
- El kutusu titriyor: `HOLD_FRAMES` ve `REQ_STABLE` değerlerini artırın.
- Yüz ile karışıyor: Yüz dışlama eşiğini (IoU) artırın veya yüz kutusunu yüzde 20 genişletip çakışma testini tekrar yapın.
- Saat/bileklik yanıltıyor: `minNeighbors`’ı 4–6 yapın, `minSize`’ı büyütün.

### Lisans / Teşekkür
- Bu proje `OpenCvSharp` kullanır (`OpenCV` bağları).
- Haar cascade dosyaları OpenCV ekosistemi kaynaklıdır.
