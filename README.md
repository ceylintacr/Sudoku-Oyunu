<h1 align="center">🧩 Sudoku — Yumuşak Tema</h1>

<p align="center">
  Unity ile geliştirilmiş, sevimli pastel temalı, dokunmatik uyumlu bir Sudoku oyunu.<br>
  Klasik Sudoku'ya ek olarak <b>“Dal (Branch)”</b> modu ile denemelerini ana tahtayı bozmadan yapabilirsin.
</p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.0.76f1-black?logo=unity" alt="Unity">
  <img src="https://img.shields.io/badge/Platform-WebGL%20%7C%20Android-7aa874" alt="Platform">
  <img src="https://img.shields.io/badge/Dil-C%23-5a3a22" alt="C#">
</p>

---

## 🎮 Oyna

> Yayınlandığında itch.io linki buraya eklenecek:
> **[▶ Tarayıcıda Oyna](#)** *(yakında)*

---

## 📸 Ekran Görüntüleri

<p align="center">
  <img src="docs/menu.png"     alt="Ana menü"   width="30%">
  &nbsp;&nbsp;
  <img src="docs/gameplay.png" alt="Oyun ekranı" width="30%">
  &nbsp;&nbsp;
  <img src="docs/win.png"      alt="Kazanma ekranı" width="30%">
</p>

---

## ✨ Özellikler

- 🧠 **Saf C# Sudoku motoru** — bulmaca üretimi, çözücü ve *tek-çözüm* garantisi
- 🎚️ **5 zorluk seviyesi:** Başlangıç · Acemi · Tecrübeli · Uzman · Profesyonel
- 🎨 **Zorluğa göre değişen pastel tema** (yeşilden kırmızıya doğru)
- ❤️ **Can sistemi** — 3 yanlışta oyun biter (3 kalp)
- 💡 **İpucu hakkı** — her seviyede 3 ipucu (ampul ikonları)
- 🌿 **Dal (Branch) modu** — “ya şu olursa?” denemelerini güvenle yap, sonra onayla
- 🎉 **Animasyonlar** — satır/sütun/3×3 kutu tamamlanınca dalga efekti, kazanınca konfeti
- ⏱️ Süre sayacı, **Geri Al**, **Sil**, **Yeni Oyun**
- 📱 Dikey/mobil için tasarlanmış arayüz + **özel WebGL şablonu** (favicon dahil)

---

## 🕹️ Nasıl Oynanır

| Eylem | Açıklama |
|------|----------|
| **Hücre seç** | Boş bir kareye dokun |
| **Sayı gir** | Alttaki 1–9 tuşlarından birine dokun |
| **Sil** | Seçili karedeki sayıyı temizler |
| **Geri Al** | Son hamleyi geri alır |
| **İpucu** | Bir kareyi doğru değeriyle doldurur (hakkın azalır) |
| **Dal Aç** | Deneme moduna girer; tekrar basınca onaylar |
| **Kalpler** | Canın — 3 yanlışta oyun biter |

Amaç: Her satır, sütun ve 3×3 kutuya 1–9 rakamlarını tekrarsız yerleştirmek.

---

## 🚀 Projeyi Açma

1. Projeyi **Unity 6 (6000.0.76f1)** ile aç.
2. `Assets/Scenes/SampleScene.unity` sahnesini aç ve **Play**'e bas.

> ℹ️ Sahne boş görünse bile sorun değil — `SudokuBootstrapper` tüm arayüzü Play
> sırasında koddan otomatik kurar.

---

## 🏗️ Build Alma

- **WebGL (tarayıcı):** `File > Build Profiles > Web > Build`
  *(WebGL Template olarak `Sudoku` seçilidir — favicon + başlık dahil.)*
- **Android:** `File > Build Profiles > Android > Build`
  *(Dikey yön ve paket adı ayarlıdır.)*

---

## 🗂️ Proje Yapısı

```
Assets/
├─ Scripts/
│  ├─ SudokuEngine.cs        # Oyun mantığı (üretim/çözüm) — MonoBehaviour değil
│  ├─ SudokuGridManager.cs   # Durum, dal sistemi, can & geri al
│  ├─ SudokuUIManager.cs     # Hücre/ızgara görselleştirme
│  └─ SudokuBootstrapper.cs  # Tüm UI'yı koddan kuran + temayı yöneten katman
├─ Scenes/SampleScene.unity
└─ WebGLTemplates/Sudoku/    # Özel WebGL şablonu (favicon)
```

---

## 🛠️ Yapım

**Unity 6 · C# · Universal Render Pipeline (2D)**

<p align="center"><sub>❤️ ile yapıldı</sub></p>
