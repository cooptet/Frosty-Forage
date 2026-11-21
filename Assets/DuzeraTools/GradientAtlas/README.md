# GradientAtlas

A Unity editor-only tool for painting a grid of gradients in the Inspector and baking them into a single square PNG atlas. Useful for gradient-mapped shaders, stylised lighting ramps and palette textures.

## Setup

Right-click in the Project window and choose **Create ▸ DuzeraTools ▸ Gradient Atlas**. No additional setup is required.

## Namespace

All scripts are under the `DuzeraTools.GradientAtlas` namespace. If you reference the atlas from your own editor scripts, add:

```csharp
using DuzeraTools.GradientAtlas;
```

## Atlas Settings

| Field | Description |
|---|---|
| Atlas Resolution | Size of the square PNG, in pixels |
| Gradient Width | Width of each gradient cell. Columns = resolution / width |
| Gradient Height | Height of each gradient cell. Rows = resolution / height |
| Auto Update | Re-bakes the atlas whenever a value changes |

The grid of gradient slots resizes itself to fill the atlas. Each cell is drawn vertically, the same way it is baked: `t = 0` at the bottom, `t = 1` at the top. Slot 0 is the top-left cell.

## Buttons

| Button | Description |
|---|---|
| Generate/Update Atlas | Bakes `<AssetName>_atlas.png` next to the asset (clamp wrap, no mipmaps). Re-baking overwrites the same PNG |
| Read From PNG | Samples each cell of the PNG and fits a gradient (up to 8 keys) to it. Use it after painting over the atlas in an image editor |

## Scripting API

| Member | Description |
|---|---|
| `Texture` | The baked PNG texture |
| `Gradients` | The gradient slots, row by row from the top-left |
| `Columns`, `Rows`, `Count` | Grid layout |
| `GetUVRect(slot)` | Normalized UV rect of a slot |
| `GetPixelRect(slot)` | Pixel rect of a slot in the imported texture |
| `UpdateAtlas()` | Same as Generate/Update Atlas |
| `ReadFromPng()` | Same as Read From PNG |

---

## Folder Structure

```text
GradientAtlas/
|-- README.md
|-- Documentation.txt
|-- Scripts/
    |-- GradientAtlasSO.cs
    |-- Editor/
        |-- GradientAtlasSOEditor.cs
```
