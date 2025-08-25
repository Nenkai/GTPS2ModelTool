# GTPS2ModelTool

*As seen on [Lifting the Bonnet on Gran Turismo's Model Format](https://nenkai.github.io/gt-modding-hub/blog/2023/11/26/lifting-bonnet-on-gt-models/)*

---

A tool that allows creating **custom models** for Gran Turismo 3. Still in early stages and may break. Other games like **GT4** are not yet supported (please contribute!)

**It is not intended to be used for extracting (aka 'ripping') models out of the game. It is a modding tool.** No support whatsoever will be provided for such purposes.

For usage documentation, refer to the [Modding Hub](https://nenkai.github.io/gt-modding-hub/ps2/models/). 

![alt text](https://pbs.twimg.com/media/F9h0TbzWMAAUP5b?format=jpg&name=small)

## Current Tasks

I am slowly retiring from GT research, but here are the current tasks left to be done

* Tex1 Optimizations - heavily needed. Some paths mentioned [here](https://github.com/Nenkai/PDTools/blob/master/PDTools.Files/Textures/PS2/TextureSet1.cs)
* Properly support reflective meshes?
* More GT3 callbacks support
* Greater PGLUmaterial support
* Figure out the bounding bit in GT3 models
* GT4 VM/Compiler?

---

## Details

There is some code for GT4 (as the codebase essentially works for both games) but still needs heavy work.

Requires **.NET 8.0** to build.

---

## Credits

* [GlitcherOG/SSX-Collection-Multitool](https://github.com/GlitcherOG/SSX-Collection-Multitool) - Useful reference for using SharpTriStrip
* Xenn - Testing

