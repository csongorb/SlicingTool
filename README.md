# SlicingTool

Release: A stable release is available on GitHub Releases.

### Overview

This is a very simple tool that converts SVG files into a leaflet PNG tile map. It takes one or more SVG images, slices them into tiles at various zoom levels and combines them into one leaflet usable structure. You can assign zoom ranges for each image to get the wanted transition between images.

### Guide:

**Input Images:**

When prompted, enter the number of SVG images you wish to process. 
Then provide the file paths to your SVG images (path and “path” are both valid)


**Output Settings:**

Specify the output directory where the tiles will be saved.
Choose to use the default tile dimension (512 pixels) or enter a custom output file size.


**Set Zoom Levels:**

For each image, input the minimum and maximum zoom level. They are not allowed to overlap since all of them will be saved to the same output folder. 
Both can be inputted at the same time by writing min-max (e.g. 1-4)

**License:**
This project is licensed under the MIT License.
