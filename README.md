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

Expand the tar archive (binaries-yyyy-mm-dd.tar.gz) to a directory, install .NET8, and give `slidegridx` execute permissions:

```shell
sudo apt install -y dotnet-runtime-8.0
chmod +x slidegridx
```

...then run it with an argument pointing to the sgx file:

```shell
./slidegridx sample.sgx
```

Requires OpenGL 4.5, and X11 is probably more reliable than Wayland.

Pull-requests are welcome.

Technically this code is compatible with Windows, too, but my [slidegrid](https://github.com/MV10/slidegrid) project provides a Windows GUI.
