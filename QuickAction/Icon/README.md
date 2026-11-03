# QuickAction icon

`QuickAction.png` is the approved transparent gradient Q/lightning artwork.
`QuickAction.ico` contains 16, 20, 24, 32, 40, 48, 64, 96, 128 and 256 pixel images and is embedded in the executable and WPF resources for the tray icon.

After replacing the source PNG, run `./scripts/Generate-AppIcons.ps1` from the repository root. This regenerates the ICO and the PNG assets in `packaging/Assets` consumed by the Store packaging script. Commit the generated assets along with source changes.
