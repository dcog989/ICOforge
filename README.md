# <img src=".\assets\icons\ICOforge.svg" width="64" height="64" align="center"> ICOforge

ICOforge is a Windows utility for converting various image formats into multi-resolution `.ico` files. It supports SVG colorization and can produce Favicon packs for use with websites.

![ICOforge](./assets/images/icoforge-screen-1.png)

## ✨ Features

- **Bulk Image Conversion:** Convert multiple image files (PNG, JPG, BMP, GIF) to the `.ico` format in a single batch.
- **Custom ICO Profiles:** Choose exactly which image sizes (x16, x32, x48, up to x256) and color depths to include in the final `.ico` file.
- **SVG Colorization:** When converting SVG icons, you can apply a custom color, allowing you to quickly generate themed icon sets from a single source file or icon library.
- **Theme Aware UI:** The application's interface automatically syncs with the Windows light or dark mode setting for a seamless, native feel.
- **Modern & Simple Interface:** A clean, fluent user interface makes the conversion process intuitive.

---

## 🛠️ Tech Stack

This application is built using a modern Windows stack:

- **Language:** **C#** - The core application logic is written in C#, the primary language for .NET development.
- **Framework:** **.NET & WPF** - The app is built on the **.NET** platform with **Windows Presentation Foundation (WPF)** for the user interface, ensuring a responsive and native experience.
- **UI Library:** **WPF-UI** - To achieve a modern look and feel, and to handle automatic light/dark theme switching, the project uses the **WPF-UI** library (<https://github.com/lepoco/wpfui>). This provides WinUI / Fluent controls and styles.
- **Raster Image Processing:** **ImageSharp** - used for reading standard image files (PNG, JPG, etc.), resizing them, and encoding the final multi-resolution `.ico` file.
- **Vector Graphics (SVG):** **Svg.Skia** - To support SVG files, **Svg.Skia** library is used. It parses SVG documents and uses SkiaSharp for rendering of the vector graphic to a bitmap, which can then be colorized and converted.
