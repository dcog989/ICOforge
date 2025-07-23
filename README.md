# ICOforge 🎨

ICOforge is a simple yet powerful Windows utility for converting various image formats into compliant, multi-resolution `.ico` files. It's built with a modern tech stack to provide a fast, native experience, including support for SVG colorization and system theme awareness.

## ✨ Features

- **Bulk Image Conversion:** Convert multiple image files (PNG, JPG, BMP, GIF) to the `.ico` format in a single batch.
- **Custom ICO Profiles:** Choose exactly which image sizes ($16 \times 16$, $32 \times 32$, $48 \times 48$, $256 \times 256$, etc.) and color depths to include in the final `.ico` file.
- **SVG Colorization:** When converting SVG icons, you can apply a custom color, allowing you to quickly generate themed icon sets from a single source file.
- **Theme Aware UI:** The application's interface automatically syncs with the Windows light or dark mode setting for a seamless, native feel.
- **Modern & Simple Interface:** A clean, fluent user interface makes the conversion process intuitive.

***

## 🛠️ Tech Stack

This application is built using a modern .NET stack for native Windows performance and easy development.

- **Language:** **C#** - The core application logic is written in C#, the primary language for .NET development.
- **Framework:** **.NET & WPF** - The app is built on the **.NET** platform with **Windows Presentation Foundation (WPF)** for the user interface, ensuring a responsive and native experience.
- **UI Library:** **WPF-UI** - To achieve a modern look and feel, and to handle automatic light/dark theme switching, the project uses the **WPF-UI** library (<https://github.com/lepoco/wpfui>). This provides WinUI-inspired controls and styles.
- **Raster Image Processing:** **ImageSharp** - This high-performance .NET library is used for reading standard image files (PNG, JPG, etc.), resizing them, and encoding the final multi-resolution `.ico` file.
- **Vector Graphics (SVG):** **Svg.Skia** - To support SVG files, the powerful **Svg.Skia** library is used. It parses SVG documents and uses SkiaSharp for high-performance rendering of the vector graphic to a bitmap, which can then be colorized and converted.
- **Development Environment:** **Visual Studio 2022** - The project is developed using Visual Studio and its integrated tools for UI design, debugging, and package management.
