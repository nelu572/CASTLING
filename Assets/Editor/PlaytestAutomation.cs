using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.LowLevel;
using UnityEngine.SceneManagement;

/// <summary>Editor-only, real PlayerInput/physics observations. See PLAYTEST_AUTOMATION.md.</summary>
[InitializeOnLoad]
public static class PlaytestAutomation
{
    [Serializable]
    public sealed class Plan
    {
        public string name = "input-sequence";
        public float timeoutSeconds = 30f;
        public string outputDirectory;
        public Step[] steps;
        public PlaytestLevelValidation.Rule[] rules;
    }

    [Serializable]
    public sealed class Step
    {
        public string name;
        public float durationSeconds = 0.5f;
        public float kingMove;
        public float rookMove;
        public bool kingJump;
        public bool rookJump;
        public bool kingCastling;
        public bool rookSlide;
        public float abilityHoldSeconds = 0.08f;
        public float jumpHoldSeconds = 0.08f;
        // "observe", "king-walk", "rook-walk", "king-jump", "rook-jump".
        public string check = "observe";
        public string subject = "king";
        public bool finishOnLanding;
    }

    [Serializable]
    public sealed class ContactObservation
    {
        public string other;
        public Vector2 point;
        public Vector2 normal;
    }

    [Serializable]
    public sealed class BodySample
    {
        public Vector2 position;
        public Vector2 velocity;
        public bool grounded;
        public bool inputActive;
        public bool movementEnabled;
        public float moveActionValue;
        public int moveActionDeviceId;
        public ContactObservation[] contacts;
        public bool contactBufferMayBeFull;
        public bool simulated;
        public float gravityScale;
        public bool slideMoving;
        public float slideDirection;
    }

    [Serializable]
    public sealed class Sample
    {
        public int tick;
        public int step;
        public double fixedTime;
        public double editorTime;
        public float timeScale;
        public BodySample king;
        public BodySample rook;
    }

    [Serializable]
    public sealed class ActionObservation
    {
        public string role;
        public string action;
        public string phase;
        public string control;
        public int deviceId;
        public int step;
        public double fixedTime;
        public double inputTime;
        public float value;
    }

    [Serializable]
    public sealed class Measurement
    {
        public string step;
        public string check;
        public string role;
        public string verdict;
        public string reason;
        public int firstTick;
        public int lastTick;
        public int samples;
        public int groundedSamples;
        public int jumpEvents;
        public int landEvents;
        public int inputPerformed;
        public int jumpInputPerformed;
        public BodySample start;
        public BodySample end;
        public Vector2 displacement;
        public float maxAbsHorizontalSpeed;
        public float sampledApexY;
        public float sampledHeight;
        public double observedAirSeconds;
        public string[] contacts;
    }

    [Serializable]
    public sealed class PlayerConditions
    {
        public string role;
        public string hierarchy;
        public int instanceId;
        public string actionMap;
        public string notification;
        public string settingsAsset;
        public string settingsJson;
        public string moveNegative;
        public string movePositive;
        public string jump;
        public string castling;
        public string slide;
        public bool originalFilterIsNull;
        public int[] originalFilterDeviceIds;
        public bool originalMapFilterIsNull;
        public int[] originalMapFilterDeviceIds;
        public bool originalNeverAutoSwitch;
        public int[] originalPairedDeviceIds;
        public BodySample initial;
        public string colliderType;
        public Vector3 scale;
        public Bounds colliderBounds;
        public float bodyGravityScale;
        public string bodyType;
        public int collisionMask;
    }

    [Serializable]
    public sealed class RunResult
    {
        public string schema = "castling-playtest/2";
        public string runId;
        public string state;
        public string reason;
        public string startedUtc;
        public string endedUtc;
        public string scenePath;
        public string controllerPath;
        public string unityVersion;
        public string inputUpdateMode;
        public string inputSettingsJson;
        public float fixedDeltaTime;
        public Vector2 gravity;
        public float initialTimeScale;
        public bool initialRunInBackground;
        public bool applicationFocused;
        public bool sceneWasDirty;
        public bool directBodyWrites = false;
        public string sampling = "After each Physics2DFixedUpdate; apex is a sampled lower bound. Input changes are quantized to InputSystem updates; transition ticks remain in samples with step=-1.";
        public string scope = "Observed sequence in the recorded terrain/contact conditions; not a maximum ability, puzzle completion, difficulty or human play assessment.";
        public string resultPath;
        public Plan plan;
        public PlayerConditions[] players;
        public int virtualKeyboardId;
        public List<Sample> samples = new List<Sample>();
        public List<ActionObservation> actions = new List<ActionObservation>();
        public List<Measurement> measurements = new List<Measurement>();
        public List<string> events = new List<string>();
        public List<string> cleanup = new List<string>();
        public bool cleanupSucceeded;
        public bool ownedDeviceAbsent;
        public bool postStopAuditPerformed;
        public bool postStopOwnedDeviceAbsent;
        public float finalTimeScale;
        public BodySample[] finalPlayers;
        public string isolationMode = "caller-managed-current-play-world";
        public string isolationCase;
        public bool enterPlayModeOptionsEnabled;
        public string enterPlayModeOptions;
        public string levelVerdict = "not_configured";
        public List<PlaytestLevelValidation.RuleResult> levelRules = new List<PlaytestLevelValidation.RuleResult>();
        public List<PlaytestLevelValidation.RuleEvidence> ruleEvidence = new List<PlaytestLevelValidation.RuleEvidence>();
    }

    private sealed class Player
    {
        internal string role;
        internal Rigidbody2D body;
        internal Collider2D collider;
        internal PlayerMovement movement;
        internal PlayerInput input;
        internal InputActionAsset actions;
        internal InputDevice[] filter;
        internal bool nullFilter;
        internal InputDevice[] mapFilter;
        internal bool nullMapFilter;
        internal bool neverSwitch;
        internal Key negative;
        internal Key positive;
        internal Key jump;
        internal InputAction moveAction;
        internal InputAction jumpAction;
        internal InputAction castlingAction;
        internal InputAction slideAction;
        internal RookSlideAbility slide;
        internal CastlingAbility castling;
        internal Key castlingKey;
        internal Key slideKey;
        internal Action<InputAction.CallbackContext> actionCallback;
        internal Action jumped;
        internal Action landed;
        internal int jumps;
        internal int lands;
        internal int performed;
        internal int jumpPerformed;
    }

    private sealed class Session
    {
        internal RunResult result;
        internal RoomTransitionController controller;
        internal Player[] players;
        internal Keyboard keyboard;
        internal Keyboard previousKeyboard;
        internal InputSettings originalSettings;
        internal InputSettings temporarySettings;
        internal bool settingsChanged;
        internal bool backgroundChanged;
        internal bool finishing;
        internal double startedEditorTime;
        internal int stepIndex;
        internal bool applied;
        internal double stepStartedFixed;
        internal int firstTick;
        internal int tick;
        internal int jumpBaseline;
        internal int landBaseline;
        internal int performedBaseline;
        internal int jumpInputBaseline;
        internal BodySample stepStart;
        internal HashSet<string> contacts = new HashSet<string>();
        internal string pendingError;
        internal int stableGroundedTicks;
        internal PlaytestLevelValidation.Evaluator level;
    }

    private sealed class PhysicsObservationMarker { }
    private const string LastPathKey = "CASTLING.PlaytestAutomation.LastResult";
    private const string OwnedPathsKey = "CASTLING.PlaytestAutomation.PendingStopAudits";
    private const int MaxSamples = 20000;
    private static Session active;
    public static bool IsRunning => active != null;

    internal static void MarkIsolation(string mode, string caseName)
    {
        if (active == null) return;
        active.result.isolationMode = mode; active.result.isolationCase = caseName;
        Write(active.result);
    }

    static PlaytestAutomation()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
        EditorApplication.quitting += BeforeQuit;
    }

    public static string Describe(int controllerInstanceId = 0)
    {
        try
        {
            RoomTransitionController controller = ResolveController(controllerInstanceId);
            Player[] players = ResolvePlayers(controller);
            return JsonUtility.ToJson(new Description
            {
                scenePath = controller.gameObject.scene.path,
                controllerInstanceId = controller.GetInstanceID(),
                controllerPath = PathOf(controller.transform),
                playing = EditorApplication.isPlaying,
                players = players.Select(Conditions).ToArray()
            }, true);
        }
        catch (Exception exception) { return Reply("rejected", exception.Message); }
    }

    [Serializable]
    private sealed class Description
    {
        public string scenePath;
        public int controllerInstanceId;
        public string controllerPath;
        public bool playing;
        public PlayerConditions[] players;
    }

    public static string BasicPlanJson(int direction = 1)
    {
        if (direction != -1 && direction != 1) return Reply("rejected", "direction must be -1 or 1.");
        return JsonUtility.ToJson(new Plan
        {
            name = "basic-walk-jump",
            steps = new[]
            {
                new Step { name = "settle", durationSeconds = 0.6f },
                new Step { name = "king-walk", durationSeconds = 0.3f, kingMove = direction, check = "king-walk" },
                new Step { name = "king-stop", durationSeconds = 0.3f },
                new Step { name = "king-jump", durationSeconds = 2f, kingJump = true, check = "king-jump", finishOnLanding = true },
                new Step { name = "rook-walk", durationSeconds = 0.3f, rookMove = direction, check = "rook-walk" },
                new Step { name = "rook-stop", durationSeconds = 0.3f, subject = "rook" },
                new Step { name = "rook-jump", durationSeconds = 2f, rookJump = true, check = "rook-jump", finishOnLanding = true },
                new Step { name = "release", durationSeconds = 0.3f }
            }
        }, true);
    }

    public static string StartBasic(int direction = 1, int controllerInstanceId = 0)
    {
        return Start(BasicPlanJson(direction), controllerInstanceId);
    }

    public static string Start(string planJson, int controllerInstanceId = 0)
    { return StartInternal(planJson, controllerInstanceId, false); }

    internal static string StartBatchCase(string planJson)
    { return StartInternal(planJson, 0, true); }

    private static string StartInternal(string planJson, int controllerInstanceId, bool batchOwned)
    {
        if (PlaytestLevelValidation.IsBatchRunning && !batchOwned) return Reply("rejected", "A batch owns execution; use its status/cancel API.");
        if (active != null)
        {
            active.result.events.Add("Concurrent Start rejected at " + DateTime.UtcNow.ToString("O"));
            return Reply("rejected", "A run is active; use Status/Cancel. The active run was preserved.");
        }
        Session session = null;
        Plan plan = null;
        try
        {
            plan = JsonUtility.FromJson<Plan>(planJson);
            ValidatePlan(plan);
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling)
                throw new InvalidOperationException("Start requires stable, unpaused Play Mode.");
            if (Time.timeScale <= 0f || Physics2D.simulationMode != SimulationMode2D.FixedUpdate)
                throw new InvalidOperationException("Positive timeScale and FixedUpdate Physics2D simulation are required.");
            if (InputSystem.settings.updateMode == InputSettings.UpdateMode.ProcessEventsManually)
                throw new InvalidOperationException("Manual InputSystem updates are unsupported.");
            RoomTransitionController controller = ResolveController(controllerInstanceId);
            Player[] players = ResolvePlayers(controller);
            foreach (Player player in players)
                if (!player.input.isActiveAndEnabled || !player.input.inputIsActive ||
                    !player.movement.isActiveAndEnabled || !player.body.simulated ||
                    player.body.bodyType != RigidbodyType2D.Dynamic || player.input.currentActionMap == null ||
                    !player.moveAction.enabled || !player.jumpAction.enabled)
                    throw new InvalidOperationException(player.role + " input, movement, actions and dynamic body must already be active.");
            if (players[0].actions == players[1].actions)
                throw new InvalidOperationException("Players must own distinct action instances.");
            if (plan.steps.Any(step => step.kingCastling) && (players[0].castling == null || !players[0].castling.isActiveAndEnabled || players[0].castlingAction == null || !players[0].castlingAction.enabled))
                throw new InvalidOperationException("King CASTLING requires its existing enabled ability and action.");
            if (plan.steps.Any(step => step.rookSlide) && (players[1].slide == null || !players[1].slide.isActiveAndEnabled || players[1].slideAction == null || !players[1].slideAction.enabled))
                throw new InvalidOperationException("Rook slide requires its existing enabled ability and action.");
            string directory = OutputDirectory(plan.outputDirectory);
            var result = new RunResult
            {
                runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8),
                state = "running", startedUtc = DateTime.UtcNow.ToString("O"),
                scenePath = controller.gameObject.scene.path, controllerPath = PathOf(controller.transform),
                unityVersion = Application.unityVersion, inputUpdateMode = InputSystem.settings.updateMode.ToString(),
                inputSettingsJson = JsonUtility.ToJson(InputSystem.settings), fixedDeltaTime = Time.fixedDeltaTime,
                gravity = Physics2D.gravity, initialTimeScale = Time.timeScale,
                initialRunInBackground = Application.runInBackground, applicationFocused = Application.isFocused,
                sceneWasDirty = controller.gameObject.scene.isDirty, plan = plan,
                players = players.Select(Conditions).ToArray(), enterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                enterPlayModeOptions = EditorSettings.enterPlayModeOptions.ToString()
            };
            result.resultPath = Path.Combine(directory, result.runId + ".json");
            string pendingAudits = SessionState.GetString(OwnedPathsKey, "");
            SessionState.SetString(OwnedPathsKey, pendingAudits + result.resultPath + "\n");
            session = new Session { result = result, controller = controller, players = players,
                originalSettings = InputSystem.settings, previousKeyboard = Keyboard.current,
                startedEditorTime = EditorApplication.timeSinceStartup };
            active = session;
            session.level = new PlaytestLevelValidation.Evaluator(plan.rules, controller, players[0].body, players[1].body, result);
            // Install timeout first so partial setup failures also have a cleanup path.
            EditorApplication.update += EditorTick;
            foreach (Player player in players) player.input.neverAutoSwitchControlSchemes = true;
            session.temporarySettings = UnityEngine.Object.Instantiate(session.originalSettings);
            session.temporarySettings.hideFlags = HideFlags.HideAndDontSave;
            session.temporarySettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            session.temporarySettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = session.temporarySettings;
            session.settingsChanged = true;
            if (!Application.runInBackground) { Application.runInBackground = true; session.backgroundChanged = true; }
            session.keyboard = InputSystem.AddDevice<Keyboard>("CASTLINGPlaytest-" + result.runId);
            result.virtualKeyboardId = session.keyboard.deviceId;
            foreach (Player player in players)
            {
                player.actions.devices = new InputDevice[] { session.keyboard };
                player.moveAction.actionMap.devices = new InputDevice[] { session.keyboard };
                BindKeys(player, session.keyboard);
                if (player.role == "king" && plan.steps.Any(step => step.kingCastling)) player.castlingKey = ResolveKey(session.keyboard, Binding(player.castlingAction, null));
                if (player.role == "rook" && plan.steps.Any(step => step.rookSlide)) player.slideKey = ResolveKey(session.keyboard, Binding(player.slideAction, null));
                player.actionCallback = context => ObserveAction(session, player, context);
                player.moveAction.performed += player.actionCallback;
                player.moveAction.canceled += player.actionCallback;
                player.jumpAction.performed += player.actionCallback;
                if (player.castlingKey != Key.None) player.castlingAction.performed += player.actionCallback;
                if (player.slideKey != Key.None)
                { player.slideAction.performed += player.actionCallback; player.slideAction.canceled += player.actionCallback; }
                player.jumped = () => player.jumps++;
                player.landed = () => player.lands++;
                player.movement.Jumped += player.jumped;
                player.movement.Landed += player.landed;
            }
            Key[] allKeys = players.SelectMany(player => new[] { player.negative, player.positive, player.jump, player.castlingKey, player.slideKey }).Where(key => key != Key.None).ToArray();
            if (allKeys.Distinct().Count() != allKeys.Length)
                throw new InvalidOperationException("Overlapping player keyboard bindings cannot be independently replayed by this tool.");
            InstallPhysicsObserver();
            InputSystem.onBeforeUpdate += BeforeInput;
            InputSystem.onAfterUpdate += AfterInput;
            Write(result);
            return Status();
        }
        catch (Exception exception)
        {
            if (session != null && active == session) Finish("failed", "Setup: " + exception.Message);
            else SaveRejection(exception.Message, plan == null ? null : plan.outputDirectory);
            return Reply("rejected", exception.Message);
        }
    }

    public static string Status()
    {
        if (active == null) return Reply("idle", SessionState.GetString(LastPathKey, ""));
        return Reply("running", active.result.runId + "; step=" + active.stepIndex + "/" + active.result.plan.steps.Length +
            "; samples=" + active.result.samples.Count + "; result=" + active.result.resultPath);
    }

    public static string Cancel(string reason = "Explicit cancellation")
    {
        if (active == null) return Reply("idle", "No active run.");
        Finish("cancelled", reason);
        return Status();
    }

    public static string Result()
    {
        if (active != null) return JsonUtility.ToJson(active.result, true);
        string path = SessionState.GetString(LastPathKey, "");
        return File.Exists(path) ? File.ReadAllText(path) : Reply("idle", "No result in this Editor session.");
    }

    private static void ValidatePlan(Plan plan)
    {
        if (plan == null || plan.steps == null || plan.steps.Length == 0 || plan.steps.Length > 256)
            throw new ArgumentException("Plan requires 1..256 steps.");
        if (!Finite(plan.timeoutSeconds) || plan.timeoutSeconds <= 0f || plan.timeoutSeconds > 300f)
            throw new ArgumentException("timeoutSeconds must be finite and in (0,300].");
        PlaytestLevelValidation.Validate(plan.rules);
        if (plan.steps.Any(step => step == null || string.IsNullOrEmpty(step.name)) || plan.steps.Select(step => step.name).Distinct().Count() != plan.steps.Length)
            throw new ArgumentException("Steps require unique nonempty names for scoped rules and comparison.");
        foreach (Step step in plan.steps)
        {
            if (step == null || !Finite(step.durationSeconds) || step.durationSeconds <= 0f || step.durationSeconds > 60f ||
                !Finite(step.kingMove) || !Finite(step.rookMove) ||
                (step.kingMove != -1f && step.kingMove != 0f && step.kingMove != 1f) ||
                (step.rookMove != -1f && step.rookMove != 0f && step.rookMove != 1f) ||
                !Finite(step.jumpHoldSeconds) || step.jumpHoldSeconds <= 0f || !Finite(step.abilityHoldSeconds) || step.abilityHoldSeconds <= 0f)
                throw new ArgumentException("Steps require finite duration (0,60], move -1/0/1 and positive jumpHoldSeconds.");
            if (step.check != "observe" && step.check != "king-walk" && step.check != "rook-walk" &&
                step.check != "king-jump" && step.check != "rook-jump")
                throw new ArgumentException("Unknown step check: " + step.check);
            if (step.subject != "king" && step.subject != "rook")
                throw new ArgumentException("subject must be king or rook (checks infer their named role).");
            bool king = step.check.StartsWith("king", StringComparison.Ordinal);
            if (step.check.EndsWith("walk", StringComparison.Ordinal) && (king ? step.kingMove : step.rookMove) == 0f)
                throw new ArgumentException("Walk check requires movement input.");
            if (step.check.EndsWith("jump", StringComparison.Ordinal) && !(king ? step.kingJump : step.rookJump))
                throw new ArgumentException("Jump check requires jump input.");
            if (step.finishOnLanding && !step.check.EndsWith("jump", StringComparison.Ordinal))
                throw new ArgumentException("finishOnLanding requires a jump check.");
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static RoomTransitionController ResolveController(int id)
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("No active loaded scene.");
        RoomTransitionController[] candidates = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<RoomTransitionController>())
            .Where(controller => controller.isActiveAndEnabled).ToArray();
        if (id != 0)
        {
            RoomTransitionController explicitController = candidates.FirstOrDefault(controller => controller.GetInstanceID() == id);
            if (explicitController == null) throw new InvalidOperationException("Controller is not active in the active scene.");
            return explicitController;
        }
        GameObject selected = Selection.activeGameObject;
        if (selected != null && selected.scene == scene)
        {
            RoomTransitionController selectedController = selected.GetComponentInParent<RoomTransitionController>();
            if (candidates.Contains(selectedController)) return selectedController;
        }
        if (candidates.Length != 1)
            throw new InvalidOperationException("Active scene requires one controller, an explicit controller instance ID, or selection of its hierarchy.");
        return candidates[0];
    }

    private static Player[] ResolvePlayers(RoomTransitionController controller)
    {
        var serialized = new SerializedObject(controller);
        Scene scene = controller.gameObject.scene;
        var players = new List<Player>();
        foreach (string role in new[] { "king", "rook" })
        {
            var body = serialized.FindProperty(role).objectReferenceValue as Rigidbody2D;
            if (body == null || body.gameObject.scene != scene || !body.gameObject.activeInHierarchy)
                throw new InvalidOperationException(role + " body must be an active reference in the active scene.");
            var movement = body.GetComponent<PlayerMovement>();
            var input = body.GetComponent<PlayerInput>();
            var collider = body.GetComponent<Collider2D>();
            if (movement == null || input == null || input.actions == null || collider == null)
                throw new InvalidOperationException(role + " requires PlayerMovement, PlayerInput/actions and Collider2D.");
            InputActionMap map = input.currentActionMap ?? input.actions.FindActionMap(input.defaultActionMap, false);
            if (map == null) throw new InvalidOperationException(role + " action map missing.");
            InputAction move = map.FindAction("Move", true);
            InputAction jump = map.FindAction("Jump", true);
            ReadOnlyArray<InputDevice>? devices = input.actions.devices;
            ReadOnlyArray<InputDevice>? mapDevices = RawMapDevices(map);
            players.Add(new Player { role = role, body = body, collider = collider, movement = movement,
                input = input, actions = input.actions, nullFilter = !devices.HasValue,
                filter = devices.HasValue ? devices.Value.ToArray() : null,
                nullMapFilter = !mapDevices.HasValue, mapFilter = mapDevices.HasValue ? mapDevices.Value.ToArray() : null,
                neverSwitch = input.neverAutoSwitchControlSchemes, moveAction = move, jumpAction = jump });
            Player resolved = players[players.Count - 1];
            resolved.castling = body.GetComponent<CastlingAbility>(); resolved.slide = body.GetComponent<RookSlideAbility>();
            resolved.castlingAction = map.FindAction("Castling", false); resolved.slideAction = map.FindAction("Slide", false);
        }
        if (players[0].body == players[1].body) throw new InvalidOperationException("King and Rook reference the same body.");
        return players.ToArray();
    }

    private static string Binding(InputAction action, string part)
    {
        var paths = new List<string>();
        foreach (InputBinding binding in action.bindings)
            if (!binding.isComposite && (part == null ? !binding.isPartOfComposite : binding.isPartOfComposite && binding.name == part) &&
                !string.IsNullOrEmpty(binding.effectivePath) && binding.effectivePath.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase))
                paths.Add(binding.effectivePath);
        if (paths.Count != 1) throw new InvalidOperationException(action + " requires one effective Keyboard binding for " + (part ?? "button") + ".");
        return paths[0];
    }

    // InputActionMap.devices returns map override ?? asset.devices. Read the raw nullable
    // override so cleanup preserves inheritance rather than pinning the effective asset list.
    // Input System 1.18 has no public getter for this distinction; fail closed if internals change.
    private static ReadOnlyArray<InputDevice>? RawMapDevices(InputActionMap map)
    {
        FieldInfo field = typeof(InputActionMap).GetField("m_Devices", BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null) throw new NotSupportedException("InputSystem map device override introspection is unavailable.");
        object storage = field.GetValue(map);
        MethodInfo getter = field.FieldType.GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
        if (getter == null || getter.ReturnType != typeof(ReadOnlyArray<InputDevice>?))
            throw new NotSupportedException("InputSystem map device override layout changed.");
        object value = getter.Invoke(storage, null);
        return value == null ? (ReadOnlyArray<InputDevice>?)null : (ReadOnlyArray<InputDevice>)value;
    }

    private static void BindKeys(Player player, Keyboard keyboard)
    {
        player.negative = ResolveKey(keyboard, Binding(player.moveAction, "negative"));
        player.positive = ResolveKey(keyboard, Binding(player.moveAction, "positive"));
        player.jump = ResolveKey(keyboard, Binding(player.jumpAction, null));
        if (!player.moveAction.controls.Any(control => control.device == keyboard) ||
            !player.jumpAction.controls.Any(control => control.device == keyboard))
            throw new InvalidOperationException(player.role + " bindings/masks exclude the test keyboard.");
    }

    private static Key ResolveKey(Keyboard keyboard, string path)
    {
        var control = keyboard.TryGetChildControl<KeyControl>(path.Substring(path.IndexOf('/') + 1));
        if (control == null) throw new InvalidOperationException("Unsupported Keyboard control: " + path);
        return control.keyCode;
    }

    private static PlayerConditions Conditions(Player player)
    {
        var settings = new SerializedObject(player.movement).FindProperty("settings").objectReferenceValue;
        return new PlayerConditions
        {
            role = player.role, hierarchy = PathOf(player.body.transform), instanceId = player.body.GetInstanceID(),
            actionMap = player.moveAction.actionMap.name, notification = player.input.notificationBehavior.ToString(),
            settingsAsset = AssetDatabase.GetAssetPath(settings), settingsJson = settings == null ? null : JsonUtility.ToJson(settings),
            moveNegative = Binding(player.moveAction, "negative"), movePositive = Binding(player.moveAction, "positive"), jump = Binding(player.jumpAction, null),
            castling = player.castlingAction == null ? null : string.Join(",", player.castlingAction.bindings.Select(binding => binding.effectivePath)),
            slide = player.slideAction == null ? null : string.Join(",", player.slideAction.bindings.Select(binding => binding.effectivePath)),
            originalFilterIsNull = player.nullFilter,
            originalFilterDeviceIds = player.filter == null ? new int[0] : player.filter.Select(device => device.deviceId).ToArray(),
            originalMapFilterIsNull = player.nullMapFilter,
            originalMapFilterDeviceIds = player.mapFilter == null ? new int[0] : player.mapFilter.Select(device => device.deviceId).ToArray(),
            originalNeverAutoSwitch = player.neverSwitch,
            originalPairedDeviceIds = player.input.devices.Select(device => device.deviceId).ToArray(),
            initial = Read(player), colliderType = player.collider.GetType().Name,
            colliderBounds = player.collider.bounds, scale = player.body.transform.lossyScale,
            bodyGravityScale = player.body.gravityScale, bodyType = player.body.bodyType.ToString(),
            collisionMask = Physics2D.GetLayerCollisionMask(player.body.gameObject.layer)
        };
    }

    private static BodySample Read(Player player)
    {
        var points = new ContactPoint2D[16];
        int count = Application.isPlaying ? player.body.GetContacts(points) : 0;
        var contacts = new List<ContactObservation>();
        for (int index = 0; index < count; index++)
        {
            ContactPoint2D point = points[index];
            Collider2D other = point.collider != null && point.collider.attachedRigidbody != player.body
                ? point.collider : point.otherCollider;
            if (other != null) contacts.Add(new ContactObservation { other = PathOf(other.transform), point = point.point, normal = point.normal });
        }
        return new BodySample { position = player.body.position,
            velocity = player.body.linearVelocity, grounded = Application.isPlaying && player.movement.HasGroundContact,
            inputActive = player.input.inputIsActive, movementEnabled = player.movement.isActiveAndEnabled,
            moveActionValue = player.moveAction.ReadValue<float>(),
            moveActionDeviceId = player.moveAction.activeControl == null ? 0 : player.moveAction.activeControl.device.deviceId,
            contacts = contacts.ToArray(), contactBufferMayBeFull = count == points.Length,
            simulated = player.body.simulated, gravityScale = player.body.gravityScale,
            slideMoving = player.slide != null && player.slide.IsMoving, slideDirection = player.slide == null ? 0 : player.slide.Direction };
    }

    private static void BeforeInput()
    {
        Session session = active;
        if (session == null || session.finishing || !GameInputUpdate()) return;
        try
        {
            if (session.keyboard == null || !session.keyboard.added) throw new InvalidOperationException("Owned keyboard was removed.");
            Step step = session.result.plan.steps[session.stepIndex];
            double elapsed = session.applied ? Time.fixedTimeAsDouble - session.stepStartedFixed : 0;
            var keys = new List<Key>(4);
            for (int index = 0; index < session.players.Length; index++)
            {
                Player player = session.players[index];
                float move = index == 0 ? step.kingMove : step.rookMove;
                bool jump = index == 0 ? step.kingJump : step.rookJump;
                if (move != 0f) keys.Add(move < 0f ? player.negative : player.positive);
                if (jump && elapsed < step.jumpHoldSeconds) keys.Add(player.jump);
                if (player.role == "king" && step.kingCastling && elapsed < step.abilityHoldSeconds) keys.Add(player.castlingKey);
                if (player.role == "rook" && step.rookSlide) keys.Add(player.slideKey);
            }
            InputSystem.QueueStateEvent(session.keyboard, new KeyboardState(keys.ToArray()));
            if (!session.applied)
            {
                session.stepStartedFixed = Time.fixedTimeAsDouble;
                session.firstTick = session.tick;
                Player subject = Subject(session);
                session.jumpBaseline = subject.jumps;
                session.landBaseline = subject.lands;
                session.performedBaseline = subject.performed;
                session.jumpInputBaseline = subject.jumpPerformed;
                session.stepStart = Read(subject);
                session.stableGroundedTicks = 0;
                session.contacts.Clear();
            }
        }
        catch (Exception exception) { session.pendingError = exception.ToString(); }
    }

    private static bool GameInputUpdate() => InputState.currentUpdateType == InputUpdateType.Dynamic ||
        InputState.currentUpdateType == InputUpdateType.Fixed;

    private static void AfterInput()
    {
        if (active == null || active.finishing || !GameInputUpdate()) return;
        active.applied = true;
    }

    private static Player Subject(Session session) => session.players[
        StepRole(session.result.plan.steps[session.stepIndex]) == "rook" ? 1 : 0];

    private static string StepRole(Step step) => step.check == "observe" ? step.subject :
        (step.check.StartsWith("rook", StringComparison.Ordinal) ? "rook" : "king");

    private static void ObserveAction(Session session, Player player, InputAction.CallbackContext context)
    {
        if (active != session || session.finishing) return;
        try
        {
            if (context.performed) player.performed++;
            if (context.performed && context.action == player.jumpAction) player.jumpPerformed++;
            session.result.actions.Add(new ActionObservation { role = player.role, action = context.action.name,
                phase = context.phase.ToString(), control = context.control.path, deviceId = context.control.device.deviceId,
                step = session.stepIndex, fixedTime = Time.fixedTimeAsDouble, inputTime = context.time,
                value = context.ReadValue<float>() });
        }
        catch (Exception exception) { session.pendingError = exception.ToString(); }
    }

    private static void PhysicsTick()
    {
        Session session = active;
        if (session == null || session.finishing) return;
        try
        {
            if (!ValidTargets(session)) throw new InvalidOperationException("Active scene, controller or player state changed.");
            string conditionsChanged = ChangedConditions(session);
            if (conditionsChanged != null) throw new InvalidOperationException(conditionsChanged);
            if (session.result.samples.Count >= MaxSamples) throw new InvalidOperationException("Sample budget exceeded.");
            var sample = new Sample { tick = session.tick++, step = session.applied ? session.stepIndex : -1,
                fixedTime = Time.fixedTimeAsDouble, editorTime = EditorApplication.timeSinceStartup,
                timeScale = Time.timeScale, king = Read(session.players[0]), rook = Read(session.players[1]) };
            session.result.samples.Add(sample);
            string ruleFailure = session.level.Evaluate(sample.tick, sample.step, "physics");
            if (ruleFailure != null) { Finish("failed", ruleFailure); return; }
            if (!session.applied) return;
            Player subject = Subject(session);
            var contacts = new ContactPoint2D[16];
            int count = subject.body.GetContacts(contacts);
            for (int index = 0; index < count; index++)
            {
                Collider2D other = contacts[index].collider;
                if (other != null && other.attachedRigidbody != subject.body) session.contacts.Add(PathOf(other.transform));
                other = contacts[index].otherCollider;
                if (other != null && other.attachedRigidbody != subject.body) session.contacts.Add(PathOf(other.transform));
            }
            Step step = session.result.plan.steps[session.stepIndex];
            double elapsed = sample.fixedTime - session.stepStartedFixed;
            session.stableGroundedTicks = subject.movement.HasGroundContact && Mathf.Abs(subject.body.linearVelocity.y) <= 0.1f
                ? session.stableGroundedTicks + 1 : 0;
            bool landed = step.finishOnLanding && subject.jumps > session.jumpBaseline &&
                subject.lands > session.landBaseline && session.stableGroundedTicks >= 2;
            if (elapsed + 0.000001 < step.durationSeconds && !landed) return;
            CompleteStep(session);
            session.stepIndex++;
            session.applied = false;
            if (session.stepIndex == session.result.plan.steps.Length)
            {
                bool failed = session.result.measurements.Any(measurement => measurement.verdict == "failed");
                Finish(failed ? "failed" : "completed", failed ? "One or more observations failed; inspect measurements." : "All requested steps observed under recorded conditions.");
            }
        }
        catch (Exception exception) { Finish("failed", exception.ToString()); }
    }

    private static void CompleteStep(Session session)
    {
        Step step = session.result.plan.steps[session.stepIndex];
        Player subject = Subject(session);
        Sample[] samples = session.result.samples.Where(sample => sample.step == session.stepIndex && sample.tick >= session.firstTick).ToArray();
        BodySample[] bodies = samples.Select(sample => subject.role == "king" ? sample.king : sample.rook).ToArray();
        var measurement = new Measurement { step = step.name, check = step.check, role = subject.role, firstTick = session.firstTick,
            lastTick = session.tick - 1, samples = bodies.Length, start = session.stepStart, end = Read(subject),
            jumpEvents = subject.jumps - session.jumpBaseline, landEvents = subject.lands - session.landBaseline,
            inputPerformed = subject.performed - session.performedBaseline, contacts = session.contacts.ToArray(),
            jumpInputPerformed = subject.jumpPerformed - session.jumpInputBaseline,
            verdict = "observed", reason = "Observation only; no gameplay completion assertion." };
        measurement.displacement = measurement.end.position - measurement.start.position;
        measurement.groundedSamples = bodies.Count(body => body.grounded);
        measurement.maxAbsHorizontalSpeed = bodies.Length == 0 ? 0f : bodies.Max(body => Mathf.Abs(body.velocity.x));
        measurement.sampledApexY = bodies.Length == 0 ? measurement.start.position.y : bodies.Max(body => body.position.y);
        measurement.sampledHeight = measurement.sampledApexY - measurement.start.position.y;
        measurement.observedAirSeconds = bodies.Count(body => !body.grounded) * (double)Time.fixedDeltaTime;
        if (step.check.EndsWith("walk", StringComparison.Ordinal))
        {
            float direction = subject.role == "king" ? step.kingMove : step.rookMove;
            bool delivered = bodies.Any(body => body.moveActionValue * direction > 0.1f &&
                body.moveActionDeviceId == session.result.virtualKeyboardId);
            bool valid = delivered && measurement.displacement.x * direction > 0.02f &&
                measurement.groundedSamples == measurement.samples && measurement.maxAbsHorizontalSpeed > 0.1f;
            measurement.verdict = valid ? "observed" : "failed";
            measurement.reason = valid ? "Grounded movement with actual action delivery; local terrain observation, not maximum speed/range certification." :
                "Expected grounded movement not observed (input, obstruction, grounding or terrain); do not use as general movement capability.";
        }
        if (step.check.EndsWith("jump", StringComparison.Ordinal))
        {
            bool valid = measurement.start.grounded && measurement.jumpInputPerformed > 0 && measurement.jumpEvents > 0 &&
                measurement.landEvents > 0 && measurement.sampledHeight > 0.02f && measurement.end.grounded;
            valid &= session.stableGroundedTicks >= 2;
            measurement.verdict = valid ? "observed" : "failed";
            measurement.reason = valid ? "Jumped, rose and landed through actual input; sampled apex and contacts are local conditions, not unobstructed maximum height/range." :
                "Jump/takeoff/landing or grounded start missing before step deadline; inspect samples, contacts and input observations.";
        }
        session.result.measurements.Add(measurement);
    }

    private static bool ValidTargets(Session session) => session.controller != null &&
        session.controller.gameObject.scene == SceneManager.GetActiveScene() && session.players.All(player =>
            player.body != null && player.movement != null && player.input != null && player.input.actions == player.actions &&
            player.body.gameObject.scene == SceneManager.GetActiveScene() &&
            player.body.gameObject.activeInHierarchy &&
            (player.input.currentActionMap == null || player.input.currentActionMap == player.moveAction.actionMap));

    private static string ChangedConditions(Session session)
    {
        if (Time.fixedDeltaTime != session.result.fixedDeltaTime)
            return "Measurement condition changed: fixedDeltaTime " + session.result.fixedDeltaTime + " -> " + Time.fixedDeltaTime + ". External value preserved.";
        if (Physics2D.gravity != session.result.gravity)
            return "Measurement condition changed: Physics2D.gravity. External value preserved.";
        if (Physics2D.simulationMode != SimulationMode2D.FixedUpdate)
            return "Measurement condition changed: Physics2D.simulationMode. External value preserved.";
        if (InputSystem.settings.updateMode.ToString() != session.result.inputUpdateMode)
            return "Measurement condition changed: InputSystem.updateMode. External value preserved.";
        return null;
    }

    private static void EditorTick()
    {
        Session session = active;
        if (session == null || session.finishing) return;
        if (session.pendingError != null) { Finish("failed", session.pendingError); return; }
        if (!EditorApplication.isPlaying) { Finish("cancelled", "Play Mode stopped."); return; }
        if (!ValidTargets(session)) { Finish("failed", "Structural target or active scene changed."); return; }
        string ruleFailure = session.level.Evaluate(session.tick, session.applied ? session.stepIndex : -1, "editor");
        if (ruleFailure != null) { Finish("failed", ruleFailure); return; }
        string conditionsChanged = ChangedConditions(session);
        if (conditionsChanged != null) { Finish("failed", conditionsChanged); return; }
        if (EditorApplication.timeSinceStartup - session.startedEditorTime >= session.result.plan.timeoutSeconds)
            Finish("timed_out", "Editor wall clock timeout (also covers pause/timeScale=0).");
    }

    private static void Finish(string state, string reason)
    {
        Session session = active;
        if (session == null || session.finishing) return;
        session.finishing = true;
        RunResult result = session.result;
        result.state = state;
        result.reason = reason;
        result.endedUtc = DateTime.UtcNow.ToString("O");
        result.levelVerdict = session.level == null ? "not_evaluated" : session.level.FinalizeRules(session.stepIndex >= result.plan.steps.Length);
        if (state == "failed" && result.levelVerdict == "error") result.state = "error";
        if (state == "completed" && result.levelVerdict != "passed" && result.levelVerdict != "not_configured")
        { result.state = result.levelVerdict == "error" ? "error" : result.levelVerdict == "not_evaluated" ? "not_evaluated" : "failed"; result.reason = "Level criteria: " + result.levelVerdict; }
        // A stopped run accounts for every requested step, including interrupted/unstarted work.
        for (int index = result.measurements.Count; index < result.plan.steps.Length; index++)
        {
            Step step = result.plan.steps[index];
            bool partial = index == session.stepIndex && session.applied;
            result.measurements.Add(new Measurement { step = step.name, check = step.check, role = StepRole(step),
                verdict = partial ? "partial" : "not_run", reason = reason,
                firstTick = partial ? session.firstTick : -1, lastTick = partial ? session.tick - 1 : -1,
                samples = partial ? result.samples.Count(sample => sample.step == index) : 0 });
        }
        // Stop every producer before resetting/removing its device. Each cleanup is independent.
        InputSystem.onBeforeUpdate -= BeforeInput;
        InputSystem.onAfterUpdate -= AfterInput;
        EditorApplication.update -= EditorTick;
        bool clean = true;
        Cleanup(result, "physics observer removed", RemovePhysicsObserver, ref clean);
        foreach (Player player in session.players)
        {
            Cleanup(result, player.role + " callbacks removed", () =>
            {
                if (player.actionCallback != null)
                {
                    player.moveAction.performed -= player.actionCallback;
                    player.moveAction.canceled -= player.actionCallback;
                    player.jumpAction.performed -= player.actionCallback;
                    if (player.castlingAction != null) player.castlingAction.performed -= player.actionCallback;
                    if (player.slideAction != null)
                    { player.slideAction.performed -= player.actionCallback; player.slideAction.canceled -= player.actionCallback; }
                }
                if (player.movement != null)
                {
                    if (player.jumped != null) player.movement.Jumped -= player.jumped;
                    if (player.landed != null) player.movement.Landed -= player.landed;
                }
            }, ref clean);
        }
        Cleanup(result, "owned held input released", () => { if (session.keyboard != null && session.keyboard.added) InputSystem.ResetDevice(session.keyboard); }, ref clean);
        foreach (Player player in session.players)
        {
            Cleanup(result, player.role + " input filter restored", () =>
            {
                if (player.actions != null) player.actions.devices = player.nullFilter ? (ReadOnlyArray<InputDevice>?)null : new ReadOnlyArray<InputDevice>(player.filter);
                if (player.moveAction != null) player.moveAction.actionMap.devices = player.nullMapFilter ? (ReadOnlyArray<InputDevice>?)null : new ReadOnlyArray<InputDevice>(player.mapFilter);
                if (player.input != null) player.input.neverAutoSwitchControlSchemes = player.neverSwitch;
            }, ref clean);
        }
        Cleanup(result, "owned keyboard removed", () => { if (session.keyboard != null && session.keyboard.added) InputSystem.RemoveDevice(session.keyboard); }, ref clean);
        Cleanup(result, "previous current keyboard restored", () => { if (session.previousKeyboard != null && session.previousKeyboard.added) session.previousKeyboard.MakeCurrent(); }, ref clean);
        Cleanup(result, "InputSettings restored", () =>
        {
            if (session.settingsChanged && InputSystem.settings == session.temporarySettings) InputSystem.settings = session.originalSettings;
            else if (session.settingsChanged) throw new InvalidOperationException("InputSettings replaced externally; left untouched.");
        }, ref clean);
        Cleanup(result, "temporary settings disposed", () => { if (session.temporarySettings != null && InputSystem.settings != session.temporarySettings) UnityEngine.Object.DestroyImmediate(session.temporarySettings); }, ref clean);
        Cleanup(result, "runInBackground restored", () => { if (session.backgroundChanged) Application.runInBackground = result.initialRunInBackground; }, ref clean);
        result.ownedDeviceAbsent = InputSystem.GetDeviceById(result.virtualKeyboardId) == null;
        result.finalTimeScale = Time.timeScale;
        Cleanup(result, "post-cleanup player observation", () =>
        {
            result.finalPlayers = session.players.Select(player => player.body == null || player.movement == null || player.input == null ? null : Read(player)).ToArray();
        }, ref clean);
        foreach (Player player in session.players)
        {
            Cleanup(result, player.role + " restored values verified", () =>
            {
                if (player.input == null || player.actions == null) return; // Destroyed scene owns its teardown.
                if (!SameFilter(player.actions.devices, player.nullFilter, player.filter) ||
                    !SameFilter(RawMapDevices(player.moveAction.actionMap), player.nullMapFilter, player.mapFilter) ||
                    player.input.neverAutoSwitchControlSchemes != player.neverSwitch)
                    throw new InvalidOperationException("Input filter/neverAutoSwitch restoration mismatch.");
            }, ref clean);
        }
        result.cleanupSucceeded = clean && result.ownedDeviceAbsent;
        result.events.Add("No transform, velocity, timeScale, physics settings, selection, scene or asset writes were performed by the run.");
        active = null;
        SessionState.SetString(LastPathKey, result.resultPath);
        try { Write(result); }
        catch (Exception exception) { Debug.LogError("Playtest result write failed: " + exception.Message); }
    }

    private static void Cleanup(RunResult result, string name, Action action, ref bool success)
    {
        try { action(); result.cleanup.Add(name + ": ok"); }
        catch (Exception exception) { success = false; result.cleanup.Add(name + ": " + exception.Message); }
    }

    private static bool SameFilter(ReadOnlyArray<InputDevice>? current, bool wasNull, InputDevice[] original)
    {
        return wasNull ? !current.HasValue : current.HasValue && current.Value.Select(device => device.deviceId)
            .SequenceEqual(original.Select(device => device.deviceId));
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Finish("cancelled", "Play Mode stopped by user/Editor.");
        if (state == PlayModeStateChange.EnteredEditMode) AuditAfterStop();
    }

    private static void BeforeReload() => Finish("interrupted", "Assembly/domain reload.");
    private static void BeforeQuit() => Finish("interrupted", "Editor quitting.");

    private static void AuditAfterStop()
    {
        string[] paths = SessionState.GetString(OwnedPathsKey, "").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        var remaining = new List<string>();
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            try
            {
                RunResult result = JsonUtility.FromJson<RunResult>(File.ReadAllText(path));
                if (result.virtualKeyboardId == 0) continue;
                result.postStopAuditPerformed = true;
                result.postStopOwnedDeviceAbsent = InputSystem.GetDeviceById(result.virtualKeyboardId) == null;
                result.events.Add("EnteredEditMode device audit: " + result.postStopOwnedDeviceAbsent);
                Write(result);
            }
            catch (Exception exception) { remaining.Add(path); Debug.LogWarning("Playtest Stop audit failed: " + exception.Message); }
        }
        SessionState.SetString(OwnedPathsKey, string.Join("\n", remaining));
    }

    private static void InstallPhysicsObserver()
    {
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        RemoveMarker(ref loop);
        if (!InsertMarker(ref loop)) throw new InvalidOperationException("Physics2DFixedUpdate not found in PlayerLoop.");
        PlayerLoop.SetPlayerLoop(loop);
    }

    private static bool InsertMarker(ref PlayerLoopSystem loop)
    {
        if (loop.subSystemList == null) return false;
        for (int index = 0; index < loop.subSystemList.Length; index++)
        {
            if (loop.subSystemList[index].type == typeof(UnityEngine.PlayerLoop.FixedUpdate.Physics2DFixedUpdate))
            {
                var systems = loop.subSystemList.ToList();
                systems.Insert(index + 1, new PlayerLoopSystem { type = typeof(PhysicsObservationMarker), updateDelegate = PhysicsTick });
                loop.subSystemList = systems.ToArray();
                return true;
            }
            PlayerLoopSystem child = loop.subSystemList[index];
            if (InsertMarker(ref child)) { loop.subSystemList[index] = child; return true; }
        }
        return false;
    }

    private static void RemovePhysicsObserver()
    {
        PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
        RemoveMarker(ref loop);
        PlayerLoop.SetPlayerLoop(loop);
    }

    private static void RemoveMarker(ref PlayerLoopSystem loop)
    {
        if (loop.subSystemList == null) return;
        loop.subSystemList = loop.subSystemList.Where(system => system.type != typeof(PhysicsObservationMarker)).ToArray();
        for (int index = 0; index < loop.subSystemList.Length; index++)
        {
            PlayerLoopSystem child = loop.subSystemList[index];
            RemoveMarker(ref child);
            loop.subSystemList[index] = child;
        }
    }

    private static string OutputDirectory(string relative)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".work"));
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", string.IsNullOrEmpty(relative)
            ? ".work/" + DateTime.Now.ToString("yyyyMMdd") + "-playtest-automation" : relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Output must be a task subfolder of project .work.");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Write(RunResult result) => File.WriteAllText(result.resultPath, JsonUtility.ToJson(result, true));

    private static void SaveRejection(string reason, string outputDirectory = null)
    {
        try
        {
            var result = new RunResult { runId = Guid.NewGuid().ToString("N"), state = "rejected", reason = reason,
                startedUtc = DateTime.UtcNow.ToString("O"), endedUtc = DateTime.UtcNow.ToString("O"), cleanupSucceeded = true, ownedDeviceAbsent = true };
            result.resultPath = Path.Combine(OutputDirectory(outputDirectory), result.runId + ".json");
            Write(result);
            SessionState.SetString(LastPathKey, result.resultPath);
        }
        catch (Exception exception) { Debug.LogWarning("Playtest rejection recording failed: " + exception.Message); }
    }

    private static string PathOf(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; }
        return path;
    }

    [Serializable]
    private sealed class Response { public string state; public string message; }
    private static string Reply(string state, string message) => JsonUtility.ToJson(new Response { state = state, message = message });
}
