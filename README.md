# slidegridx

A simple multi-monitor or tiled-window slideshow Linux program for events or parties.

Features:
* Configurable display areas, either full-screen or defined regions
* Automatic or manual advance
* Synchronized or staggered auto advance
* Highlight list shown more often, or exclusively
* Multiple randomization options

Configuration is by a text *.sgx file. Instructions are in the sample.sgx included with the program.

The Content list is always required. The Highlight list is optional.

Keys to control the presentation are shown in the console.

Currently, all images are zoomed to their display region with correct aspect ratio.

The program supports JPG, PNG, GIF, and BMP files.

Expand the tar archive (binaries-yyyy-mm-dd.tar.gz) to a directory, [install .NET8](https://learn.microsoft.com/en-us/dotnet/core/install/linux), and give `slidegridx` execute permissions:

```shell
# you may need to add Microsoft to your apt source list
sudo apt install -y dotnet-runtime-8.0
chmod +x slidegridx
```

...then run it with an argument pointing to the sgx file:

```shell
./slidegridx sample.sgx
```

Pull-requests are welcome.

Requires OpenGL 4.5, and X11 is probably more reliable than Wayland. It will attempt to use XWayland if X11 is not available. If you see blank grid squares on Wayland, it's probably a known NVIDIA driver bug and only X11 will fix it. Isn't Linux fun?

Another quirk of Linux is that the window compositor is asynchronous but OpenGL is synchronous. The individual grid squares are separate windows, so it's almost impossible to force perfect synchronization. My test data is 9 grid squares loading wallpaper images up to 2K in size, and they all update in about 0.5 seconds. 

Technically this code is compatible with Windows, too, but my [slidegrid](https://github.com/MV10/slidegrid) project provides a Windows GUI (with native Windows API rather than OpenGL, so it is highly responsive).
