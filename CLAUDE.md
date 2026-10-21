# Slide Surfer

Мобильный раннер для Android (Unity 6, URP). Серфер на доске едет по водной горке, уклоняется от препятствий
и перепрыгивает их, прыгая с бортика. Геймдизайн-документ: `C:\Users\gulzh\Adeka\Knigi\GDesignDocument (1).pdf`
(читать через `pdftotext -enc UTF-8`). Общение с пользователем на русском.

## Окружение
- Unity **6000.6.6f1**, URP (качество по умолчанию — Balanced), новая Input System.
- Проект обычно открыт в редакторе, работаю через **Unity MCP** (`mcp__unity-mcp__*`): `Unity_RunCommand` выполняет C# в редакторе,
  `Unity_GetConsoleLogs` читает консоль. Batchmode не запустится, пока редактор открыт.
- В Player Settings включён **Run In Background**, поэтому Play mode идёт, даже когда Unity не в фокусе. Перед релизом под Android
  её стоит выключить или обработать паузу при сворачивании.
- Коммиты пользователь делает сам через GitHub Desktop (ветка `develop`), не пушит. Самому не коммитить без просьбы.
- Компиляцию без Unity можно проверить через dotnet: netstandard2.1-проект со ссылками на
  `C:/Program Files/Unity/Hub/Editor/6000.6.6f1/Editor/Data/Managed/UnityEngine/*.dll` и **копии** DLL из
  `Library/ScriptAssemblies` (не сами файлы!), с `UseSharedCompilation=false`, `-nodeReuse:false` и потом
  `dotnet build-server shutdown`. Иначе фоновые процессы dotnet держат `Library/ScriptAssemblies/*.dll`, Unity не может
  их перезаписать, считает, что есть ошибки компиляции, и билд падает с «Build completed with a result of 'Unknown'».
- Быстрая проверка билда без полной сборки: `PlayerBuildInterface.CompilePlayerScripts` для активной платформы (Android).

## Как смотреть результат
- Снимки делаю рендером камеры в RenderTexture → PNG в scratchpad → `Read`. Перед снимком с `Camera.main` выключать `Canvas`
  (Screen Space Overlay иначе окрашивает кадр).
- Скиннинг в edit mode не обновляется сразу: чтобы увидеть позу из клипа, `AnimationMode.SampleAnimationClip`, затем
  `SkinnedMeshRenderer.BakeMesh(mesh, false)` во временный MeshRenderer.
- Воде в edit mode нужны глобалки: `_WaterFlowOffset` (float) и `_WaterPlayerPos` (xyz = низ доски, w = 1).

## Структура
- `Assets/Scirpts/` (опечатка в названии папки — так и есть, не переименовывать)
  - `MainGame/EventManager.cs` — статические события `PlayerDied`, `GameHasStarted` (один раз за забег, флаг `GameStarted`,
    `ResetState()` при рестарте), `PlayerJumped`.
  - `MainGame/SpawnObstacles.cs` + `ObjectPool.cs` + `PosGenerate.cs` — спавн препятствий из `Resources/Prefabs`.
  - `MainGame/GoPipeGo.cs` — движение труб и препятствий к игроку (−Z) со скоростью `GameSettings.currentSpeed`.
  - `MainGame/PlayerAnimator.cs` — параметры аниматора (Lean, Airborne, JumpSpeed, Trick), события `Jumped`/`Landed`.
  - `MainGame/SurferSpray.cs` — брызги из-под доски и всплески (частицы строятся в коде).
  - `MainGame/FlowingWater.cs` — сетка воды по форме трубы (лучи вниз по коллайдерам с тегом `Pipe`) и глобалки шейдера.
  - `MainGame/ObstacleVariant.cs` — у одиночного препятствия выбирает вид: «Мокрый пол» в центре, ограждение сбоку.
  - `Input/MoveInput.cs` — управление игроком, смерть при столкновении с тегом `Obstacle`.
- `Assets/GameSettings.asset` — скорость и интервал спавна (ScriptableObject).
- `Assets/Scenes/MainGame.unity` — единственная сцена.

## Генераторы (Editor, меню Tools → SlideSurfer)
Визуал создаётся скриптами, а не руками. Чтобы что-то поменять, правь генератор и перезапусти его:
- **Setup Surfer Visual** (`Assets/Editor/SurferSetup.cs`) — префаб `Assets/Surfer/SurferVisual.prefab`, процедурные клипы и
  `SurferController`. Модель: `Assets/Models/SurferRFRigged.fbx` (Mixamo, Generic). Позы задаются структурой `Pose` →
  `Stance()` в абсолютных углах в системе координат персонажа (X — его правая сторона/хвост доски, Z — куда смотрит грудь);
  есть мини-FK-решатель `Rig`. Трюки: ShoveIt, Kickflip, Spin360 (случайный, без повтора подряд). Ассеты обновляются на месте
  (удаление и пересоздание по тому же пути ломало ссылку на контроллер).
- **Add Water** (`Assets/Editor/WaterSetup.cs`) — объект `Water`, материалы `Assets/Water/Water.mat` и `Spray.mat`, компонент
  `SurferSpray` на игроке. Повторный запуск сбрасывает материал воды к дефолтам шейдера.
- **Build Obstacle Models** (`Assets/Editor/ObstacleSetup.cs`) — модели препятствий внутри префабов (дочерний `Visual`),
  белые кубы остаются невидимыми хитбоксами. Материалы и текстуры в `Assets/Obstacles/`.

- **Setup Coins** (`Assets/Editor/CoinSetup.cs`) — префаб `Resources/Prefabs/Coin.prefab` (крутится дочерний `Model`, корень
  не вращать — GoPipeGo двигает его в локальных осях), иконка `Assets/Coins/CoinIcon.png`, счётчик `CoinCounter` внизу слева
  на Canvas, `CoinSpawner` на SpawnManager.

- **Build Shark** (`Assets/Editor/SharkSetup.cs`) — модель акулы в `Resources/Prefabs/Shark.prefab` (тело-лофт, плавники,
  хвост на отдельном пивоте), куб `SharkObj` остаётся невидимым хитбоксом. Ассеты в `Assets/Shark/`. Сетки обновляются через
  Mesh API: `EditorUtility.CopySerialized` для Mesh оставлял старую геометрию на GPU.

## Звук
- **Setup Audio** (`Assets/Editor/AudioSetup.cs`) синтезирует SFX в `Assets/Audio/SFX/*.wav` (монета, прыжок, приземление,
  удар, джингл проигрыша, предупреждение и всплеск акулы, клик, петля воды), находит точки цикла музыки из
  `Assets/Audio/Music/`, ставит импорт (музыка Streaming/Vorbis, SFX DecompressOnLoad/ADPCM mono), кладёт `AudioManager` в сцену.
- `AudioManager` (DontDestroyOnLoad, музыка не прерывается при рестарте): цикл кроссфейдом двух AudioSource между
  loopStart/loopEnd; low-pass «под водой» до старта и после смерти; монеты повышают тон по серии; вода громче/выше со скоростью.
  События: `EventManager`, `Coin.Removed`, `SharkMover.Appeared/Leaped`, `PlayerAnimator.Jumped/Landed`; клик из `UIManager`.
  Громкости `MusicVolume/SfxVolume` в PlayerPrefs (`volume_music`, `volume_sfx`) — для будущих настроек.

## Меню
- **Setup Menus** (`Assets/Editor/UISetup.cs`) строит на Canvas: окно настроек внутри `SettingsMenu` (`SettingsPanel`:
  музыка/эффекты вкл-выкл + громкость, графика Low/Medium/High = уровни качества 0/1/2, во время забега — пауза
  через `Time.timeScale = 0`), кнопку магазина `ShopButton` внизу справа (прячется на `GameHasStarted`) и окно
  `MessagePopup` («Will be added soon!»). Спрайты (скруглённый прямоугольник, круг, сумка) рисуются кодом в `Assets/UI/Generated`.
- `UIInputGuard` на Canvas: пока открыто окно или касание началось на UI, `MoveInput` не двигает игрока и не стартует забег.
- Шрифт LiberationSans без кириллицы — тексты UI на английском.

## Темп (пейсинг)
- Задаёт `SpawnObstacles` (на SpawnManager), время = секунды забега = очки. Кривые `SpeedOverTime` (11→17→25→33→40 на
  0/60/120/240/360 с) и `IntervalOverTime` (2.4→1.7→1.25→0.95→0.8 с) пишут `GameSettings.currentSpeed/currentSB`;
  `ScoreCounter` скорость больше не трогает.
- Разогрев 6 с без препятствий; первое полное ограждение на 22 с (учит прыжку); первая акула гарантированно на `SharkFromScore`.
- Не больше двух одинаковых подряд; полное ограждение только если прыжок (`JumpNearBorder.CooldownLeft`) будет готов к его
  приходу; с 100 с зигзаги (два одиночных подряд на разных дорожках); каждые 22–32 с передышка 3.5 с с цепочкой монет.
- `SpeedFov` на камере расширяет FOV с ростом скорости (с ~20 до 38 ед/с, до +9°).
- Галочка `LogPacing` пишет каждый спавн в консоль. Проверка: игрок в Rigidbody.isKinematic под землю + `Time.timeScale = 8`.

## Акула
- Появляется с `SpawnObstacles.SharkFromScore` (70). Двигает её `SharkMover` (без физики и GoPipeGo), вдвое быстрее горки:
  по центру плывёт навстречу игроку наполовину в воде; сбоку ждёт под землёй за стенкой и, когда до игрока 48 м,
  перепрыгивает стенку дугой и приземляется на противоположную сторону (x = ∓2.2) за 20 м до игрока, дальше плывёт там.
- `SlideProfile` (static) — общая форма сечения горки для монет и акулы (лучи по коллайдерам `Pipe`).

## Монеты
- `CoinWallet` (static): `Total` хранится в PlayerPrefs под ключом `coins_total` (для будущего магазина, тратить через
  `TrySpend`), `RunCoins` — за текущий забег. Сохранение при смерти, паузе и выходе, не на каждый подбор.
- `CoinSpawner` кладёт цепочки по 7–13 монет (змейка, дуга от стенки к стенке, завиток) по форме дна горки.
  Монету, на которую наехало препятствие, прячет сама монета (`Coin.OnTriggerEnter` с тегом `Obstacle`).
- После тестов в Play mode сбрасывать `coins_total` (выход из Play сохраняет кошелёк через `OnApplicationQuit`).

## Шейдеры (`Assets/Water/`)
- `FlowingWater.shader` — вода: рябь, каустика, пена у стенок, кильватерный след, блики.
- `Spray.shader` — мягкие капли для частиц.
- `PipeWall.shader` — горка «парикмахерский столб»: полосы ColorA/ColorB, период полос должен делить 4 м (две трубы 96 и 88 м).
  Значения в `PipeWall.mat` пользователь настраивает сам — не сбрасывать без спроса.

## Что ещё не сделано
- До старта забега вода видна за ближним краем трубы.
- Пол/окружение — плоская бирюзовая плоскость.
