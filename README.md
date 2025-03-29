# ZenWindowMover

**ZenWindowMover** is a convenient extension for ZenBrowser that allows you to move the browser window by dragging any header element, a manually specified class, or the top 50 pixels of the webpage. This feature is inspired by the **"Allow window dragging from the top of webpages"** feature of the ARC browser, significantly enhancing browser usability.
---

## Demo (Clickable preview to YouTube video example)

[![Watch the video](https://img.youtube.com/vi/R8LKdQqqFpE/maxresdefault.jpg)](https://www.youtube.com/watch?v=R8LKdQqqFpE)
---

## Functionality

- **Window Dragging:** Move the browser window by dragging any specified area (header, class, or top 50px).  
- **Double Click for Fullscreen:** Double-click the top part of the website to maximize the window. Double-click again to restore the windowed mode.  
- **Snap to Top for Fullscreen:** Move the window to the top edge of the screen to maximize it. Pull the window down if it is already maximized to restore the windowed mode.  

---

## How It Works

ZenWindowMover works by combining two components:

1. **Firefox Extension**:
   - Searches for elements on the page, such as headers, manually specified class names, or the top 50 pixels of the page.
   - When you interact with these elements, it sends the cursor coordinates to the C# server.

2. **C# Server**:
   - Receives the coordinates from the extension.
   - Uses the WinAPI to move the main browser window according to the received cursor coordinates.

---

## Installation

1. **Download the latest release**:
   - Go to the [Releases page](https://github.com/dvgmdvgm/ZenWindowMover/releases) and download the latest version.

2. **Install the extension**:
   - Open ZenBrowser and navigate to:  
     ```
     about:debugging#/runtime/this-firefox
     ```
   - Click **"Load Temporary Add-on"** and select the **manifest.json** file from the **ZenWindowMover (FireFox Extension)** folder.

3. **Start the server**:
   - Run **ZenWindowMover.exe** (requires .NET Framework version 4.6 or higher).

---

## Configuration

- To enable window dragging by a specific class name:
  1. Create a text file in the **movable** folder.
  2. Write the class name inside the file.
  3. Rename the file to **domainexample.com.txt**.
  4. Restart the server and the browser.

- If the **domainexample.com.txt** file does not exist, the extension will try to find a default header to make it movable.  
- If no header is found, the extension will make the top 50 pixels of the webpage movable.

---

## License

This project is licensed under the MIT License. See the `LICENSE` file for more information.
