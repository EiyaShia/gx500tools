# gx500tools

Software pendamping untuk gamepad **Rexus GX500** di Windows. GX500 tidak punya software resmi; app ini menambahkan remap tombol, turbo, macro, gyro, dan profil per game. Ringan (± 800 KB) dan sepenuhnya offline.

> Proyek komunitas, tidak berafiliasi dengan Rexus.

## Fitur

- **Remap tombol** — ubah fungsi tombol mana pun, atau nonaktifkan.
- **Turbo** per tombol, 2–30 tekan per detik.
- **Gyro** (mode Switch) → gerak mouse atau analog kanan. Sensitivitas, invert, deadzone, smoothing, kalibrasi drift, dan mode aktif (selalu / saat tombol ditahan / toggle).
- **Macro** — rekam langsung dari gamepad atau tulis sebagai teks. Mode sekali jalan, ulang selama ditahan, atau toggle.
- **Gamepad → keyboard & mouse** — tombol ke key/mouse button, analog kiri → WASD, analog kanan → mouse.
- **Profil per game** — profil berganti otomatis sesuai game (`.exe`) yang sedang aktif.
- **Tester** — cek tombol, trigger, drift analog, deadzone, dan getar.
- Berjalan di **system tray**, bisa otomatis mulai bersama Windows.

## Cara kerja

```
GX500 ──► GX500 Tool (remap, turbo, macro, gyro) ──► Virtual Xbox 360 controller ──► Game
   └── disembunyikan dari game lewat HidHide (mode Switch), supaya input tidak dobel
```

## Mode GX500

GX500 punya 3 mode. Mode yang disarankan: **Switch**.

| Mode | Remap / turbo / macro | Gyro | Controller asli bisa disembunyikan | Getar dari game |
|---|---|---|---|---|
| **Switch** | ✅ | ✅ | ✅ | belum |
| X-Input | ✅ | ❌ | ❌ (game bisa melihat 2 controller) | ✅ |
| D-Input | ❌ | ❌ | — | — |

Catatan: dua tombol tambahan di bawah analog kanan adalah tombol macro hardware. Secara default keduanya mengirim D-pad Kiri/Kanan, sehingga tidak bisa dibedakan dari D-pad di software.

## Instalasi

1. Instal kebutuhan berikut (sekali saja):
   - [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (x64)
   - [ViGEmBus](https://github.com/nefarius/ViGEmBus/releases) — driver virtual controller
   - [HidHide](https://github.com/nefarius/HidHide/releases) — untuk menyembunyikan controller asli; **restart Windows** setelah instal

   Atau lewat terminal:
   ```
   winget install Microsoft.DotNet.DesktopRuntime.8
   winget install ViGEm.ViGEmBus
   winget install Nefarius.HidHide
   ```
2. Unduh `gx500tools-x.y.z.exe` dari halaman [Releases](https://github.com/EiyaShia/gx500tools/releases) dan jalankan.

## Cara pakai

1. Pindahkan GX500 ke **mode Switch** dan colokkan.
2. Buka app, klik **Aktifkan** di sidebar. Virtual Xbox 360 controller akan muncul.
3. Di halaman **Remap & Turbo**, klik **Sembunyikan controller asli** (minta izin admin sekali).
4. Atur remap, gyro, macro, atau keyboard sesuai kebutuhan. Semua tersimpan otomatis.
5. Untuk profil per game: buka game-nya, kembali ke halaman **Profil**, lalu klik **Tambah \<game\>.exe**.

Menutup jendela tidak menghentikan app; app tetap berjalan di tray. Untuk keluar: klik kanan ikon tray → **Keluar**.

Profil dan pengaturan tersimpan di `%AppData%\GX500Tool`.

### Format langkah macro

Satu baris per langkah: `TOMBOL DURASI_MS`. Gabungkan tombol dengan `+`, dan pakai `-` untuk jeda.

```
Down 30
Down+Right 30
Right+X 50
- 100
```

Nama tombol: `A B X Y LB RB LT RT Back Start LS RS Guide Up Down Left Right Capture`.

## Build dari source

Butuh [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project src/GX500Tool
```

Build satu file exe:

```
dotnet publish src/GX500Tool -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -o dist
```

## Terima kasih

- [ViGEmBus](https://github.com/nefarius/ViGEmBus) dan [HidHide](https://github.com/nefarius/HidHide) oleh Nefarius
- [HidSharp](https://www.zer7.com/software/hidsharp)
- Dokumentasi protokol Switch Pro Controller dari komunitas reverse-engineering Nintendo Switch dan driver Switch di SDL
