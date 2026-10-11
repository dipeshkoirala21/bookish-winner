# Reference brief: graphics quality that fits the phone (package `quality`)

The owner asked: *"Depending upon which phone the game is being played, shouldn't it render that kind of quality?"*
This brief records what is actually in Nepali pockets in 2023-2026, how Unity names their GPUs, and the table that maps them to the game's three tiers (ARCHITECTURE.md section 10). The code is `game/Assets/Ghumante/Core/Platform/` (`GpuTierTable`, `DeviceClassifier`, `QualityGovernor`, `QualityChoices`); the phone table is unit-tested in `core-tests/QualityClassifierTests.cs`.

## 1. Who plays in Nepal

StatCounter, mobile vendor share in Nepal, September 2026: **Apple 25 %, Samsung 21.7 %, Xiaomi (incl. Redmi/POCO) 21.2 %, Vivo 6.9 %, Oppo 5.6 %, Realme 4.8 %** (source 1). Gadgetbyte Nepal also names Xiaomi and Samsung the unit leaders in 2025, with Nothing and Honor growing fastest (source 2). Retail price lists from Nepali dealers (September-October 2026, sources 3-7) show what is on sale now; the big sellers are the budget and mid-range lines below NPR 50,000.

| Brand | Models on sale in Nepal 2025-2026 (price list) | SoC | GPU (renderer string) |
|---|---|---|---|
| Samsung | Galaxy A07 / A07s / A08 | Helio G99 | Mali-G57 MC2 |
| Samsung | Galaxy A05, A06 (2024-25) | Helio G85 | Mali-G52 MC2 |
| Samsung | Galaxy A16 / A17 5G, M17 5G | Exynos 1330 | Mali-G68 MP2 |
| Samsung | Galaxy A25 / A35 / A26 / M36 | Exynos 1280 / 1380 | Mali-G68 MP4 / MP5 |
| Samsung | Galaxy A27 5G, A36 | Snapdragon 6 Gen 3 | Adreno 710 |
| Samsung | Galaxy A55, A37 | Exynos 1480 | Xclipse 530 |
| Samsung | Galaxy A56, A57 | Exynos 1580 / 1680 | Xclipse 540 / 550 |
| Samsung | Galaxy S24/S25 FE, S26 FE | Exynos 2400 / 2500 | Xclipse 940 / 950 |
| Samsung | Galaxy S25/S26 series, Z Fold/Flip | Snapdragon 8 Elite (Gen 5) | Adreno 830 / 840 |
| Xiaomi | Redmi A5 / A7 / A7 Pro | Unisoc T7250 | Mali-G57 MP1 |
| Xiaomi | Redmi 14C / 15C | Helio G81-Ultra | Mali-G52 MC2 |
| Xiaomi | Redmi 15, Redmi Note 13 4G | Snapdragon 685 | Adreno 610 |
| Xiaomi | Redmi 17 | Helio G91-Ultra | Mali-G52 MC2 |
| Xiaomi | Redmi Note 17 5G | Snapdragon 4 Gen 4 | Adreno 613 |
| Xiaomi | Redmi Note 14 5G | Dimensity 7025-Ultra | Mali-G57 MC2 |
| Xiaomi | Redmi Note 14 Pro 5G | Dimensity 7300-Ultra | Mali-G615 MC2 |
| Xiaomi | Redmi Note 13 Pro 5G / Note 17 Pro | Snapdragon 7s Gen 2 / 6s Gen 4 | Adreno 710 |
| Xiaomi | Redmi Note 14 Pro+ / Note 17 Pro Max | Snapdragon 7s Gen 3 / 6 Gen 5 | Adreno 810 (812) |
| Xiaomi | Xiaomi 14T / 17T | Dimensity 8300 / 8500 | Mali-G615 MC6 / Mali-G720 MC8 |
| Xiaomi | Xiaomi 15 / 17 Ultra | Snapdragon 8 Elite (Gen 5) | Adreno 830 / 840 |
| Realme | C61, C63, Narzo N61 | Unisoc T612 | Mali-G57 MP1 |
| Realme | C67 5G, 12x 5G, Narzo N65 | Dimensity 6100+ / 6300 | Mali-G57 MC2 |
| Realme | 15T 5G (8-12 GB) | Dimensity 6400 | Mali-G57 MC2 |
| Realme | Narzo 70 Pro | Dimensity 7050 | Mali-G68 MC4 |
| Realme | 12 Pro+ | Snapdragon 7s Gen 2 | Adreno 710 |
| Realme | GT 6T | Snapdragon 7+ Gen 3 | Adreno 732 |
| Vivo | Y19s | Unisoc T612 | Mali-G57 MP1 |
| Vivo | Y28 | Helio G85 | Mali-G52 MC2 |
| Vivo | Y29, V30/V40/V50 Lite 4G (8 GB) | Snapdragon 685 | Adreno 610 |
| Vivo | V50 Lite 5G (12 GB) | Dimensity 6300 | Mali-G57 MC2 |
| Vivo | V30 / V40 / V50 (12 GB) | Snapdragon 7 Gen 3 | Adreno 720 |
| Vivo | V60 5G | Snapdragon 7 Gen 4 | Adreno 722 |
| Vivo | X200 Pro / X300 Pro | Dimensity 9400 / 9500 | Immortalis-G925 / Mali-G1-Ultra MC12 |
| Oppo | A3x, A5, A5x, A5 Pro, A3 | Snapdragon 6s 4G Gen 1 | Adreno 610 |
| Oppo | A18, A38, A58 | Helio G85 | Mali-G52 MC2 |
| Oppo | A17, A57, A77 (older, still sold) | Helio G35 | PowerVR GE8320 |
| Oppo | A60, A78, A77s | Snapdragon 680 | Adreno 610 |
| Oppo | Reno 10 / 11F | Dimensity 7050 | Mali-G68 MC4 |
| Oppo | Reno 12 / Reno 15 | Dimensity 7300 / 7400 | Mali-G615 MC2 |
| Oppo | Reno 14F / 15F | Snapdragon 6 Gen 1 | Adreno 710 |
| Oppo | Find N5 | Snapdragon 8 Elite | Adreno 830 |
| Apple | iPhone 11 (A13, 4 GB), SE 2 (3 GB) | A13 | Apple A13 GPU |
| Apple | iPhone 12 to 17 | A14 to A19 | Apple A14+ GPU |

**What this means for the game.** Most phones in Nepal are budget Android with a weak GPU, and many of them now ship with 8-12 GB of RAM (Redmi 15C 8 GB, Realme 15T 12 GB, Vivo V50 Lite 5G 12 GB) on chips whose GPU is a Mali-G57 MC2 or an Adreno 610. The previous heuristic (RAM and CPU count only) rated a 12 GB Dimensity 6300 phone **High** - the exact case the owner described. The GPU table fixes it.

## 2. How Unity names the GPU

`SystemInfo.graphicsDeviceName` is the driver's renderer string: Adreno reports `Adreno (TM) 619` (both GLES and Vulkan; Unity's device filter docs use `Adreno (TM) 740`, source 10), Mali reports `Mali-G57 MC2` on many MediaTek builds but often just `Mali-G71` / `Mali-G68` without the core count (logcat examples, source 11), Samsung's AMD GPUs `Samsung Xclipse 940`, Imagination `PowerVR Rogue GE8320`, Apple `Apple A15 GPU` and on Macs `Apple M2 Pro`. The parser reads family, model number and, when present, the `MCn`/`MPn` core count; every rule has a conservative answer without the count.

## 3. The table (tier ceilings)

Relative GPU speed from notebookcheck's smartphone GPU list, GFXBench Manhattan 3.1 offscreen (fps, higher is better; source 8): Adreno 610 13, 613 19.5, 619 34, 642L 57, 644 65, 710 53.5, 810 62, 720 84, 722 124, 725 137, 732/735 ~176, 740 213, 750 247; Mali-G52 MP2 15, G57 MP1 8.8, G57 MP2 25, G68 MP2 24, G68 MP4 42, G68 MP5 52, G610 MP4 73, G610 MP6 99, G615 MP2 56, G615 MP6 163, G710 97-149, G720 MP7/8 ~199; Xclipse 530 67, 540 85, 920 125, 940 230, 950 281; PowerVR GE8322 6.5; Immortalis-G720 241.

ARCHITECTURE.md section 10 anchors the tiers: **Mid** is Snapdragon 7 Gen 1 / 7s Gen 2 (Adreno 644 / 710, about 55-65), **High** starts at Snapdragon 7 Gen 3 (Adreno 720, 84). So: under about 40 is **Low**, 40-80 **Mid**, above **High**.

| Family | Low | Mid | High |
|---|---|---|---|
| Adreno 3xx-5xx | all | | |
| Adreno 6xx | 610-620 (SD 680/685/695/4 Gen 1-2) | 630-649 (845, 855, 778G, 7 Gen 1) | 650+ (865, 888) |
| Adreno 7xx | 702 | 710-719 (6 Gen 1/3, 7s Gen 2, 6s Gen 4) | 720+ (7 Gen 3, 7 Gen 4, 7+ Gen 2/3, 8 Gen 1-3) |
| Adreno 8xx | | 810-819 (7s Gen 3, 6 Gen 5) | 820+ (8 Elite, 8 Elite Gen 5) |
| Mali Midgard (T-series), Utgard (4xx), G31/G51/G52/G57 | all | | |
| Mali-G68 | MP2-MP3 (Exynos 1330) | MP4+ and unknown count (Exynos 1280/1380, Dimensity 7050) | |
| Mali-G71/G72, G76 below MP10 | all | G76 MP10+ | |
| Mali-G77 | | all | |
| Mali-G78 | | below MP14, unknown | MP14+ |
| Mali-G6xx (610, 615, 625) | | below MP6, unknown | MP6+ (Dimensity 8200/8300) |
| Mali-G7xx (710-725), G9xx, Immortalis | | | all |
| Mali-G1 (5th gen, 2025) | | Pro | Premium, Ultra |
| Xclipse | | 530 | 540+, 9xx |
| PowerVR | Rogue (GE8320/8322), B-Series (BXM/BXE) | C-Series (CXT) | D-Series (DXT, Tensor G5) |
| Huawei Maleoon | | all | |
| Apple A | A11 and older | A12-A13 | A14+ |
| Apple M | | | all (Mac editor: 16 GB or more, else Mid) |
| Intel integrated (editor) | | all | |
| Software (SwiftShader, llvmpipe) | all | | |

Unknown families set no ceiling, so the RAM/CPU proxy alone decides (as before).

## 4. Memory and cores

Android reports RAM a little under the marketing figure: 4 GB about 3.6-3.8 GB, 6 GB about 5.5 GB, 8 GB about 7.3-7.6 GB, 12 GB about 11.4 GB. With a recognised GPU the memory ceiling is: under 5 GB Low, under 7 GB (6 GB phones) Mid, 8 GB and more High (ARCHITECTURE's High budget is 2.0 GB on 6 GB+ phones; 8 GB leaves room). Fewer than 6 cores is Low. Without a recognised GPU the older proxy stays: High only from about 10 GB. The device tier is the lower of the RAM/CPU ceiling and the GPU ceiling.

Examples (all in the unit test): Galaxy A15 8 GB (G99) **Low**; Redmi 15C 8 GB **Low**; Realme 15T 12 GB **Low**; Galaxy A25 6 GB **Mid**; Redmi Note 14 Pro 5G **Mid**; Galaxy A36 **Mid**; Galaxy A56 8 GB **High** (6 GB variant **Mid**); Vivo V50 **High**; Galaxy S23 8 GB **High** (it was Mid under the RAM-only rule); iPhone 11 **Mid**; iPhone 12 and newer **High**.

**iOS** keeps the model-identifier rule (iPhone13,x = A14 = High); iPads and unknown identifiers use the Apple GPU name, then RAM. **Mac editor**: Apple M1/M2/M3/M4 is High with 16 GB or more, Mid otherwise; Intel Macs Mid.

## 5. Settings and the adaptive governor

* **Settings > Graphics quality**: Auto (default, labelled with the tier Auto runs, "Auto (Medium)" / "स्वचालित (मध्यम)"), Low, Medium, High; the status line names the phone's tier ("Your phone: Medium") and says when Auto was lowered. **Frame rate** 30 / 60 where the tier allows (60 needs Medium or High; Low runs 30). Saved in the save's `settings` section (`graphics`, `frameRate`).
* **Applied at runtime, no restart**: quality level (URP asset: MSAA, shadows, HDR, authored render scale), target frame rate, look tier (outlines, texture size, triplanar), and the open world (streaming radii, building bands, meshing options, life caps, dressing, airport) behind a short "redrawing the streets" overlay once the sheet closes. Nothing waits for the next map load.
* **Governor** (Explore only, frames during loading, pause, menus and single hitches over 250 ms ignored): dynamic resolution between URP render scale 0.7 and the tier's authored maximum in 0.1 steps; down after 2 s of frames 12 % over budget, up after 5 s with GPU time under 70 % of budget (or 10 s on target when the driver gives no GPU timings); a failed probe doubles the next wait (5 to 120 s), so it never oscillates. On Auto only, too slow at 0.7 for about 10 s steps the tier down once with a toast ("Graphics lowered to keep things smooth" / "सहज चलाउन ग्राफिक्स घटाइयो"), remembered for this phone (`autoTierCap`, `autoTierDevice`); a step back up needs a minute with GPU time under 45 % of budget at full scale and happens at most once per session.

## 6. Open points

* `PlayerSettings.enableFrameTimingStats` should be on for release builds (ProjectSetup, look package) so FrameTimingManager reports GPU time on device; without it the governor still works from frame intervals but recovers resolution more slowly.
* Mali drivers that omit the core count: Exynos 1330 phones (Galaxy A16/A17) then classify as Mid and rely on the governor to step down; `SystemInfo.processorModel` (Unity 6, "s5e8535") could identify the SoC once the compile check's reference assemblies include it.
* Validate the cut-offs on the three reference devices of the device pass (Wave 3).

## Sources

1. StatCounter Global Stats, Mobile Vendor Market Share Nepal (Sep 2026): https://gs.statcounter.com/vendor-market-share/mobile/nepal
2. Gadgetbyte Nepal, mobiles: https://gadgetbytenepal.com/cat/mobiles/ ; TechLekh awards 2025: https://techlekh.com/techlekh-awards-best-phones-2025-nepal-winners/
3. Nepalitelecom, Samsung mobile price in Nepal (updated 8 Oct 2026): https://nepalitelecom.com/samsung-mobile-price-in-nepal
4. Nepalitelecom, Xiaomi mobile price in Nepal (updated 28 Sep 2026): https://nepalitelecom.com/xiaomi-mobile-price-in-nepal
5. Nepalitelecom, vivo mobiles price in Nepal (updated 1 Sep 2026): https://nepalitelecom.com/vivo-mobiles-price-in-nepal
6. Nepalitelecom, OPPO mobiles price in Nepal (updated 1 Sep 2026): https://nepalitelecom.com/oppo-mobiles-price-in-nepal
7. Nepalitelecom, realme mobile price in Nepal (updated 10 Oct 2026): https://nepalitelecom.com/realme-mobile-price-in-nepal ; Nepal price dataset (Hugging Face, Sanoj111/mobile_prices): https://huggingface.co/datasets/Sanoj111/mobile_prices
8. Notebookcheck, Smartphone Graphics Cards Benchmark List (GFXBench Manhattan 3.1 offscreen): https://www.notebookcheck.net/Smartphone-Graphics-Cards-Benchmark-List.149363.0.html
9. SoC GPUs: Notebookcheck Snapdragon 4 Gen 4 (Adreno 613): https://www.notebookcheck.net/Qualcomm-Snapdragon-4-Gen-4-Processor-Benchmarks-and-Specs.1353439.0.html ; Snapdragon 6s Gen 4 (Adreno 710): https://www.cpu-monkey.com/en/cpu-qualcomm_snapdragon_6s_gen_4 ; Snapdragon 6s 4G Gen 2 (Adreno 610): https://www.91mobiles.com/processor/qualcomm-snapdragon-6s-gen-2-pdp ; Snapdragon 6 Gen 5: https://nanoreview.net/ru/soc/qualcomm-snapdragon-6-gen-5 ; Dimensity 9500 (Mali-G1-Ultra MC12), 8500 (Mali-G720 MP8), Exynos 1680 (Xclipse 550): https://www.91mobiles.com/processor/samsung-exynos-1680-vs-mediatek-dimensity-9500 , https://www.notebookcheck.net/MediaTek-Dimensity-9500-now-official-with-major-upgrades-over-Dimensity-9400-to-challenge-Apple-and-Qualcomm.1120672.0.html
10. Unity, AndroidDeviceFilterData.deviceName ("Adreno (TM) 740"): https://docs.unity3d.com/ScriptReference/AndroidDeviceFilterData-deviceName.html
11. Unity forum logcat renderer lines ("Renderer: Mali-G71", "Renderer: Adreno (TM) 330"): https://discussions.unity.com/t/having-a-black-screen-out-of-nowhere-when-running-on-android-worked-this-morning/654864
