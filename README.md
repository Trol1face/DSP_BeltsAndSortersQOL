# BuildingQOLs
This mod adds a couple of optionable QOL features related to building of belts and sorters (maybe smth else later).

If anything will be bad you can report via github issues (or i'm Spiritfarer in discord), not sure i'll fix bugs but i'll try to

### Whats the options?
- ***1 click belt/sorter building.*** Default behaviour is click #1 - mark for building start point, drag mouse, click #2 - build. Mod adds another one: hold - mark, drag mouse, release - build. So you can **click - drag - click** or **hold - drag - release** to build a belt/sorter.
- ***Disable belt prolongation.*** Default belt building behaviour includes continuous belt building if you build it into nothing. End of the belt becomes a new start and you can left click for new end or right click to cancel. There is an option to disable that. Idea is to click-drag-release, click-drag-release, similar to factorio.
- ***Show current altitude.*** There is an option to replace default tip near cursor (*Choose starting position* and *Click to build*) with **Altitude: n/ Length: m** or **A: n | L: m** *(enable short ver in config)* so you will always see your current belt altitude.
- ***Auto take belt altitude.*** Starting a belt in another belt on any altitude changes your current altitude to that belt's altitude (if your current altitude is 0 and you start a belt in another belt with altitude 4 your building altitude changes to 4).
## Config
Everything is optional so you can enable/disable any in Config after first game launch with mod enabled. Everything is enabled by default except *Short version of altitude* (however i recommend the short one).
## Recent changes
#### 1.0.2
When building condition of belt is not OK cursor text with condition problem not will be under Altitude/length, not replacing them.