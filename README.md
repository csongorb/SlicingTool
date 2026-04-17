# SlicingTool

Release: A stable release is available on [GitHub Releases](https://github.com/csongorb/SlicingTool/releases/).

## Overview

This is a very simple tool that converts SVG files into a leaflet PNG tile map. It takes one or more SVG images, slices them into tiles at various zoom levels and combines them into one leaflet usable structure. You can assign zoom ranges for each image to get the wanted transition between images.

<img width=100% alt="Slicing-tool-working" src="https://github.com/user-attachments/assets/131c847f-4d4c-4865-8198-d04c5942ad74" />

<img width=100% alt="Slicing-tool-guide" src="https://github.com/user-attachments/assets/c25ad59e-8495-4a39-9b93-5f29318f2313" />

## Guide:

### Input Images:
When prompted, enter the number of SVG images you wish to process. 
Then provide the file paths to your SVG images (path and “path” are both valid)

### Output Settings:
Specify the output directory where the tiles will be saved.

### Set output iamge pixel size:
Choose to use the default tile dimension (512 pixels) or enter a custom output file size.

### Set Zoom Levels:
For each image, input the minimum and maximum zoom level. They are not allowed to overlap since all of them will be saved to the same output folder. 
Both can be inputed at the same time by writing min-max (e.g. 1-4). You can also use negative zoom levels (like -2, -4, etc.) for zooming out on very large SVGs.

### Image Compression:
The tool supports automatic image optimization for the generated PNG tiles to greatly reduce file size. This feature relies on [oxipng](https://github.com/oxipng/oxipng), an external PNG optimizer.
For it to work, ensure that the `oxipng.exe` executable is placed in the same directory as the tool's executable (or accessible via your system's PATH environment variable).

## License:
This project is licensed under the MIT License.
