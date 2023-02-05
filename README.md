# Wii Walk
Making the Wii Balance Board a controller for all games.

https://user-images.githubusercontent.com/20060156/216798797-f15304c2-c712-4b64-82ca-e3d83ffa9560.mp4

Wii Walk allows you to turn your wii balance board into a controller, particularly for first person games. It focuses on realistic walking, turning and jumping, letting your character mimic you in the real world. Current detected motions include:
- Walking (alternating left/right weight shifts)
- Sprinting (faster weight shifts)
- Turning, including whilst walking or sprinting (overall left/right weiht shifts)
- Jumping (a loss of weight)

This project was created in under 40 hours for the UQCS 2022 Hackathon. Much thanks goes to [lshchar](https://github.com/lshachar) as this project is heavily adapted from his modification of [WiimoteLib DLL](https://github.com/lshachar/WiiBalanceWalker).

## Background
The Nintendo Wii was one of the most popular consoles of it's generation. It's innovative motion controls were ground breaking. However, some aspects of the Wii were not considered in their full potential. The Wii balance board was supported by only a handful of games, despite the potential. By asking what if and ignoring the instructions not to jump, Wii Walk allows the balance board to be used on a PC to play any game imaginable.

## How it works
The Wii balance board connect to the Wii via Bluetooth. It is treated like a special Wii mote. With scertain libraries, your laptop can do the same. Wii Walk is based off [lshachar's modification of WiimoteLib DLL](https://github.com/lshachar/WiiBalanceWalker), and takes the 4 pressure values from each of the corners. These are then analysed, to track walking, sprinting and turning (by a shifting of weight left and right). These then trigger key presses or mouse movements based on the set-up controls specified.
## How to use
Download and run `build.sh`. Wii Walk requires `msbuild.exe`, with a directory specified in `build.sh`. When running the program, add your Wii Balance Board via you OS tools, then click the "connect wii board" to sync. Once connected, the options on the side let you modiy the result of each indicated action.
