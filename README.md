# Sudoku (Branching)

Unity ile yapılmış, sevimli pastel temalı bir Sudoku oyunu. Arayüzün tamamı çalışma
zamanında koddan kurulur (`SudokuBootstrapper`), bu yüzden boş bir sahnede Play'e
basmak yeterlidir.

## Özellikler
- Saf C# Sudoku motoru (üretim, çözüm, tek-çözüm doğrulama) — `SudokuEngine`
- 5 zorluk seviyesi: Başlangıç · Acemi · Tecrübeli · Uzman · Profesyonel
- Zorluğa göre değişen pastel renk teması
- Can sistemi (3 kalp) ve zorluk başına 3 ipucu (ampul ikonları)
- **Dal (Branch) modu:** denemeleri ana tahtayı bozmadan güvenle yapma
- Satır / sütun / 3×3 kutu tamamlanınca animasyon, kazanınca konfeti + pop-up
- Süre sayacı, Geri Al, Sil, Yeni Oyun
- Mobil/dikey için tasarlanmış arayüz + özel WebGL şablonu (favicon dahil)

## Çalıştırma
1. Projeyi **Unity 6 (6000.0.76f1)** ile aç.
2. `Assets/Scenes/SampleScene.unity`'i aç ve **Play**'e bas.

> Sahne boş görünse bile sorun değil — `SudokuBootstrapper` tüm arayüzü Play
> sırasında otomatik kurar.

## Build
- **WebGL (tarayıcı):** `File > Build Profiles > Web > Build`. WebGL Template olarak
  `Sudoku` seçilidir (favicon + başlık dahil).
- **Android:** `File > Build Profiles > Android`. Dikey yön ayarlıdır.

## Proje yapısı
- `Assets/Scripts/SudokuEngine.cs` — oyun mantığı (MonoBehaviour değil)
- `Assets/Scripts/SudokuGridManager.cs` — durum, dal sistemi, can/undo
- `Assets/Scripts/SudokuUIManager.cs` — hücre/ızgara görselleştirme
- `Assets/Scripts/SudokuBootstrapper.cs` — tüm UI'yı koddan kuran ve temayı yöneten katman
- `Assets/WebGLTemplates/Sudoku/` — özel WebGL şablonu
