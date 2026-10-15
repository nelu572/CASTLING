using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>Read-only level criteria and explicitly scene-scoped repeat coordination.</summary>
[InitializeOnLoad]
public static class PlaytestLevelValidation
{
    [Serializable]
    public sealed class Rule
    {
        public string id;
        // success: required predicate; failure: forbidden predicate; trigger: recovery prerequisite.
        public string kind = "success";
        public string timing = "end"; // end, ever, always (failure/trigger always watch for a match).
        public string target = "king"; // king, rook, controller, activeRoom, startingEntry, path:<hierarchy>
        public string metric = "grounded";
        public string op = "eq";
        public float value = 1f;
        public float tolerance = 0.001f;
        public string action;
        public string step; // Optional exact step name; out-of-scope ticks cannot satisfy the rule.
        public string triggerId;
        public bool relativeToStart;
        public float holdSeconds;
        public bool requireFalseBeforeRecovery = true;
        public float withinSeconds = 3f; // Recovery uses Editor wall time, including timeScale=0.
    }

    [Serializable]
    public sealed class RuleResult
    {
        public string id;
        public string kind;
        public string status = "pending";
        public string reason;
        public int evaluations;
        public float initialValue;
        public float lastValue;
        public bool lastMatched;
        public int firstMatchedTick = -1;
        public double firstMatchedEditorTime = -1;
        public double triggeredEditorTime = -1;
        public int lastTick = -1;
        public double matchStartedEditorTime = -1;
        public bool recoveryUnmetObserved;
    }

    [Serializable]
    public sealed class RuleEvidence
    {
        public string id;
        public string phase;
        public int tick;
        public int step;
        public double editorTime;
        public float value;
        public bool matched;
        public string status;
    }

    internal sealed class Evaluator
    {
        private readonly Rule[] rules;
        private readonly RoomTransitionController controller;
        private readonly Rigidbody2D king;
        private readonly Rigidbody2D rook;
        private readonly PlaytestAutomation.RunResult run;
        private readonly Scene scene;
        private readonly Transform roomsRoot;
        private readonly Dictionary<string, RuleResult> results = new Dictionary<string, RuleResult>();
        private readonly Dictionary<string, GameObject> targets = new Dictionary<string, GameObject>();

        internal Evaluator(Rule[] definitions, RoomTransitionController owner, Rigidbody2D kingBody,
            Rigidbody2D rookBody, PlaytestAutomation.RunResult result)
        {
            rules = definitions ?? new Rule[0]; controller = owner; king = kingBody; rook = rookBody; run = result;
            scene = owner.gameObject.scene;
            roomsRoot = new SerializedObject(owner).FindProperty("roomsRoot").objectReferenceValue as Transform;
            Validate(rules);
            foreach (Rule rule in rules.OrderBy(rule => rule.kind == "trigger" ? 0 : 1))
            {
                var state = new RuleResult { id = rule.id, kind = rule.kind };
                results.Add(rule.id, state); run.levelRules.Add(state);
                try { state.initialValue = Read(rule); }
                catch (Exception exception) { state.status = "error"; state.reason = exception.Message; }
            }
        }

        internal string Evaluate(int tick, int stepIndex, string phase)
        {
            foreach (Rule rule in rules.OrderBy(rule => rule.kind == "trigger" ? 0 : 1))
            {
                RuleResult state = results[rule.id];
                if (state.status == "error") return "Level rule error " + rule.id + ": " + state.reason;
                if (!string.IsNullOrEmpty(rule.step) && (stepIndex < 0 || stepIndex >= run.plan.steps.Length || run.plan.steps[stepIndex].name != rule.step)) continue;
                try
                {
                    if (rule.kind == "recovery")
                    {
                        RuleResult trigger = results[rule.triggerId];
                        if (trigger.firstMatchedEditorTime < 0) continue;
                        if (state.triggeredEditorTime < 0) state.triggeredEditorTime = trigger.firstMatchedEditorTime;
                        if (state.status == "passed") continue;
                        if (EditorApplication.timeSinceStartup - state.triggeredEditorTime > rule.withinSeconds)
                        { state.status = "failed"; state.reason = "Recovery deadline expired after observed trigger."; return state.reason + " " + rule.id; }
                    }
                    float value = Read(rule);
                    if (rule.relativeToStart) value -= state.initialValue;
                    bool rawMatch = Matches(rule, value);
                    if (!rawMatch) { state.matchStartedEditorTime = -1; if (rule.kind == "recovery") state.recoveryUnmetObserved = true; }
                    else if (state.matchStartedEditorTime < 0) state.matchStartedEditorTime = EditorApplication.timeSinceStartup;
                    bool match = rawMatch && EditorApplication.timeSinceStartup - state.matchStartedEditorTime >= rule.holdSeconds;
                    if (rule.kind == "recovery" && rule.requireFalseBeforeRecovery && !state.recoveryUnmetObserved) match = false;
                    bool changed = state.evaluations == 0 || state.lastMatched != match || state.lastValue != value;
                    state.evaluations++; state.lastValue = value; state.lastMatched = match; state.lastTick = tick;
                    if (match && state.firstMatchedEditorTime < 0)
                    { state.firstMatchedTick = tick; state.firstMatchedEditorTime = EditorApplication.timeSinceStartup; }
                    if (rule.kind == "failure" && match) { state.status = "failed"; state.reason = "Forbidden predicate observed."; }
                    else if (rule.kind == "trigger" && match) state.status = "observed";
                    else if (rule.kind == "recovery" && match) state.status = "passed";
                    else if (rule.kind == "success" && rule.timing == "ever" && match) state.status = "passed";
                    else if (rule.kind == "success" && rule.timing == "always" && !rawMatch) { state.status = "failed"; state.reason = "Required invariant became false."; }
                    if (changed || state.status == "failed") run.ruleEvidence.Add(new RuleEvidence { id = rule.id,
                        phase = phase, tick = tick, step = stepIndex, editorTime = EditorApplication.timeSinceStartup,
                        value = value, matched = match, status = state.status });
                    if (state.status == "failed" && (rule.kind == "failure" || rule.timing == "always")) return "Level rule failed: " + rule.id;
                }
                catch (Exception exception) { state.status = "error"; state.reason = exception.Message; return "Level rule error " + rule.id + ": " + exception.Message; }
            }
            return null;
        }

        internal string FinalizeRules(bool sequenceCompleted)
        {
            foreach (Rule rule in rules)
            {
                RuleResult state = results[rule.id];
                if (state.status == "failed" || state.status == "error" || state.status == "passed") continue;
                if (rule.kind == "trigger")
                { state.status = state.firstMatchedEditorTime >= 0 ? "observed" : "not_evaluated"; continue; }
                if (!sequenceCompleted || state.evaluations == 0)
                { state.status = "not_evaluated"; state.reason = "Sequence interrupted, rule scope not sampled, or recovery trigger never observed."; continue; }
                if (rule.kind == "failure") state.status = "passed";
                else if (rule.kind == "trigger") { if (state.status != "observed") state.status = "not_evaluated"; }
                else if (rule.kind == "success")
                {
                    bool pass = rule.timing == "end" ? state.lastMatched : rule.timing == "always" && state.lastMatched;
                    state.status = pass ? "passed" : "failed";
                    state.reason = pass ? "Configured predicate observed in its declared scope." : "Required predicate was not satisfied by sequence end.";
                }
                else { state.status = "failed"; state.reason = "Triggered recovery was not observed before sequence end."; }
            }
            RuleResult[] graded = run.levelRules.Where(state => state.kind != "trigger").ToArray();
            if (run.levelRules.Any(state => state.status == "error")) return "error";
            if (graded.Length == 0) return "not_configured";
            if (graded.Any(state => state.status == "error")) return "error";
            if (graded.Any(state => state.status == "failed")) return "failed";
            if (!sequenceCompleted) return "not_evaluated";
            if (graded.Any(state => state.status == "not_evaluated")) return "not_evaluated";
            return "passed";
        }

        private GameObject Target(string selector)
        {
            if (targets.TryGetValue(selector, out GameObject cached))
            { if (cached == null || cached.scene != scene) throw new InvalidOperationException("Rule target disappeared: " + selector); return cached; }
            GameObject target;
            switch (selector)
            {
                case "king": target = king.gameObject; break;
                case "rook": target = rook.gameObject; break;
                case "controller": target = controller.gameObject; break;
                case "activeRoom": target = controller.ActiveRoom == null ? null : controller.ActiveRoom.gameObject; break;
                case "startingEntry": target = controller.StartingEntry == null ? null : controller.StartingEntry.gameObject; break;
                default:
                    if (selector == null || !selector.StartsWith("path:", StringComparison.Ordinal)) throw new ArgumentException("Unsupported target: " + selector);
                    string path = selector.Substring(5);
                    Transform[] matches = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                        .Where(transform => HierarchyPath(transform) == path).ToArray();
                    if (matches.Length != 1) throw new InvalidOperationException("Rule path must identify exactly one object in active scene: " + path);
                    target = matches[0].gameObject;
                    if (roomsRoot == null || !(target.transform == roomsRoot || target.transform.IsChildOf(roomsRoot)))
                        throw new InvalidOperationException("Explicit rule path must be inside this controller's Rooms Root.");
                    break;
            }
            if (target == null || target.scene != SceneManager.GetActiveScene()) throw new InvalidOperationException("Rule target unavailable in active scene: " + selector);
            // ActiveRoom is dynamic: do not cache its current reference.
            if (selector != "activeRoom") targets[selector] = target;
            return target;
        }

        private float Read(Rule rule)
        {
            if (rule.metric == "timeScale") return Time.timeScale;
            if (rule.metric == "playerDistance") return Vector2.Distance(king.position, rook.position);
            if (rule.metric == "exchangeXError") return Mathf.Max(Mathf.Abs(king.position.x - Initial("rook").position.x), Mathf.Abs(rook.position.x - Initial("king").position.x));
            GameObject target = Target(rule.target);
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            PlayerMovement movement = target.GetComponent<PlayerMovement>();
            PlayerInput input = target.GetComponent<PlayerInput>();
            switch (rule.metric)
            {
                case "active": return target.activeInHierarchy ? 1 : 0;
                case "x": return target.transform.position.x;
                case "y": return target.transform.position.y;
                case "deltaX": return target.transform.position.x - Initial(rule.target).position.x;
                case "deltaY": return target.transform.position.y - Initial(rule.target).position.y;
                case "velocityX": return Required(body, rule).linearVelocity.x;
                case "velocityY": return Required(body, rule).linearVelocity.y;
                case "grounded": return Required(movement, rule).HasGroundContact ? 1 : 0;
                case "movementEnabled": return Required(movement, rule).isActiveAndEnabled ? 1 : 0;
                case "inputActive": return Required(input, rule).inputIsActive ? 1 : 0;
                case "simulated": return Required(body, rule).simulated ? 1 : 0;
                case "slideMoving": return Required(target.GetComponent<RookSlideAbility>(), rule).IsMoving ? 1 : 0;
                case "slideDirection": return Required(target.GetComponent<RookSlideAbility>(), rule).Direction;
                case "switchPressed": return Required(target.GetComponent<PressurePlateSwitch>(), rule).IsPressed ? 1 : 0;
                case "gateOpen": return Required(target.GetComponent<PressureGate>(), rule).IsOpen ? 1 : 0;
                case "goalCompleted": return Required(target.GetComponent<ChapterGoal>(), rule).Completed ? 1 : 0;
                case "respawning": return Required(target.GetComponent<VoidRespawnZone>(), rule).IsRespawning ? 1 : 0;
                case "respawnCount": return Required(target.GetComponent<VoidRespawnZone>(), rule).RespawnCount;
                case "actionPerformed": return run.actions.Count(action => action.role == rule.target && action.action == rule.action && action.phase == "Performed" && action.deviceId == run.virtualKeyboardId);
                case "startDistance":
                    if (rule.target != "king" && rule.target != "rook") throw new ArgumentException("startDistance requires king/rook.");
                    Transform point = rule.target == "king" ? controller.StartingEntry.KingPoint : controller.StartingEntry.RookPoint;
                    return Vector2.Distance(target.transform.position, point.position);
                default: throw new ArgumentException("Unsupported read-only metric: " + rule.metric);
            }
        }

        private PlaytestAutomation.BodySample Initial(string role)
        {
            if (role != "king" && role != "rook") throw new ArgumentException("Position delta requires king/rook.");
            return run.players.First(player => player.role == role).initial;
        }

        private static T Required<T>(T component, Rule rule) where T : UnityEngine.Object
        { if (component == null) throw new InvalidOperationException(rule.id + " target lacks " + typeof(T).Name); return component; }
    }

    internal static void Validate(Rule[] rules)
    {
        if (rules == null) return;
        if (rules.Length > 128) throw new ArgumentException("At most 128 level rules are supported.");
        var ids = new HashSet<string>();
        foreach (Rule rule in rules)
        {
            if (rule == null || string.IsNullOrEmpty(rule.id) || !ids.Add(rule.id)) throw new ArgumentException("Level rule IDs must be nonempty and unique.");
            if (rule.kind != "success" && rule.kind != "failure" && rule.kind != "trigger" && rule.kind != "recovery") throw new ArgumentException("Unknown rule kind.");
            if (rule.timing != "end" && rule.timing != "ever" && rule.timing != "always") throw new ArgumentException("Unknown rule timing.");
            if (!new[] { "eq", "ne", "gt", "ge", "lt", "le" }.Contains(rule.op) || !Finite(rule.value) || !Finite(rule.tolerance) || rule.tolerance < 0 || !Finite(rule.holdSeconds) || rule.holdSeconds < 0)
                throw new ArgumentException("Invalid rule comparison/value/tolerance.");
            if (rule.kind == "recovery" && (!Finite(rule.withinSeconds) || rule.withinSeconds <= 0)) throw new ArgumentException("Recovery requires positive finite deadline.");
        }
        foreach (Rule rule in rules.Where(rule => rule.kind == "recovery"))
            if (!rules.Any(trigger => trigger.id == rule.triggerId && trigger.kind == "trigger")) throw new ArgumentException("Recovery must refer to a trigger rule ID.");
    }

    private static bool Matches(Rule rule, float actual)
    {
        switch (rule.op)
        {
            case "eq": return Mathf.Abs(actual - rule.value) <= rule.tolerance;
            case "ne": return Mathf.Abs(actual - rule.value) > rule.tolerance;
            case "gt": return actual > rule.value;
            case "ge": return actual >= rule.value;
            case "lt": return actual < rule.value;
            default: return actual <= rule.value;
        }
    }

    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static string HierarchyPath(Transform transform)
    { string path = transform.name; while (transform.parent != null) { transform = transform.parent; path = transform.name + "/" + path; } return path; }

    [Serializable]
    public sealed class BatchCase { public string name; public string scenePath; public PlaytestAutomation.Plan plan; }
    [Serializable]
    public sealed class BatchPlan { public string name = "scene-reload-repeat"; public string outputDirectory; public BatchCase[] cases; }
    [Serializable]
    public sealed class CaseResult { public string name; public string scenePath; public string state; public string runId; public string resultPath; public string levelVerdict; public string reason; }
    [Serializable]
    public sealed class BatchResult
    {
        public string state;
        public string verdict;
        public string reason;
        public string isolationMode = "play-scene-single-reload";
        public string isolationLimit = "Scene instances are recreated. Static/ScriptableObject/DDOL state is not reset. Explicit Stop/new Play is required for a stronger independent run; EnterPlayModeOptions remain unchanged.";
        public string enterPlayModeOptions;
        public bool enterPlayModeOptionsEnabled;
        public string initialScene;
        public bool initialSceneDirty;
        public string resultPath;
        public BatchPlan plan;
        public List<CaseResult> cases = new List<CaseResult>();
    }

    private sealed class BatchSession
    {
        internal BatchResult result;
        internal int index;
        internal string phase = "load";
        internal int loadedFrame;
        internal double loadedAt;
        internal bool originalBackground;
        internal string childPath;
        internal string childRunId;
    }

    private static BatchSession batch;
    public static bool IsBatchRunning => batch != null;
    private const string BatchPathKey = "CASTLING.PlaytestLevelValidation.LastBatch";

    static PlaytestLevelValidation()
    {
        AssemblyReloadEvents.beforeAssemblyReload += () => CancelBatch("Assembly reload interrupted batch.");
        EditorApplication.quitting += () => CancelBatch("Editor quitting.");
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) CancelBatch("Play Stop interrupted batch."); };
    }

    public static string StartBatch(string json)
    {
        if (batch != null || PlaytestAutomation.IsRunning) return "rejected: another run/batch is active";
        try
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || EditorApplication.isCompiling) throw new InvalidOperationException("Batch requires stable unpaused Play Mode; never opens an Edit scene.");
            BatchPlan plan = JsonUtility.FromJson<BatchPlan>(json);
            if (plan == null || plan.cases == null || plan.cases.Length < 1 || plan.cases.Length > 16) throw new ArgumentException("Batch requires 1..16 cases.");
            foreach (BatchCase item in plan.cases)
            {
                if (item == null || item.plan == null || string.IsNullOrEmpty(item.scenePath) || AssetDatabase.LoadAssetAtPath<SceneAsset>(item.scenePath) == null)
                    throw new ArgumentException("Each case requires a saved scene asset and plan.");
                if (string.IsNullOrEmpty(item.plan.outputDirectory)) item.plan.outputDirectory = plan.outputDirectory;
            }
            string directory = WorkDirectory(plan.outputDirectory);
            foreach (BatchCase item in plan.cases)
                if (string.IsNullOrEmpty(item.plan.outputDirectory)) item.plan.outputDirectory = directory;
            var result = new BatchResult { state = "running", initialScene = SceneManager.GetActiveScene().path,
                initialSceneDirty = SceneManager.GetActiveScene().isDirty, plan = plan,
                enterPlayModeOptions = EditorSettings.enterPlayModeOptions.ToString(), enterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled,
                resultPath = Path.Combine(directory, "batch-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".json") };
            batch = new BatchSession { result = result, originalBackground = Application.runInBackground };
            Application.runInBackground = true;
            EditorApplication.update += BatchTick;
            SaveBatch(result);
            return BatchStatus();
        }
        catch (Exception exception)
        {
            if (batch != null) try { EndBatch("error", "Setup: " + exception.Message); } catch { /* Resources were released before writing. */ }
            return "rejected: " + exception.Message;
        }
    }

    public static string StartFresh(string planJson, string scenePath)
    {
        var plan = JsonUtility.FromJson<PlaytestAutomation.Plan>(planJson);
        return StartBatch(JsonUtility.ToJson(new BatchPlan { outputDirectory = plan.outputDirectory,
            cases = new[] { new BatchCase { name = plan.name, scenePath = scenePath, plan = plan } } }));
    }

    public static string BatchStatus() => batch == null ? "idle; result=" + SessionState.GetString(BatchPathKey, "") :
        "running; case=" + batch.index + "/" + batch.result.plan.cases.Length + "; phase=" + batch.phase + "; result=" + batch.result.resultPath;

    public static string BatchResultJson()
    {
        if (batch != null) return JsonUtility.ToJson(batch.result, true);
        string path = SessionState.GetString(BatchPathKey, "");
        return File.Exists(path) ? File.ReadAllText(path) : "No batch result.";
    }

    public static string CancelBatch(string reason = "Explicit batch cancellation")
    {
        if (batch == null) return "idle";
        CancelOwnedChild(reason);
        EndBatch("cancelled", reason);
        return BatchStatus();
    }

    private static void BatchTick()
    {
        if (batch == null) return;
        try
        {
            if (!EditorApplication.isPlaying) { CancelBatch("Play Mode ended."); return; }
            BatchCase item = batch.result.plan.cases[batch.index];
            if (batch.phase == "load")
            {
                EditorSceneManager.LoadSceneInPlayMode(item.scenePath, new LoadSceneParameters(LoadSceneMode.Single));
                batch.loadedFrame = Time.frameCount; batch.loadedAt = EditorApplication.timeSinceStartup; batch.phase = "settle";
            }
            else if (batch.phase == "settle")
            {
                if (EditorApplication.timeSinceStartup - batch.loadedAt > 10) throw new TimeoutException("Loaded scene never advanced a runtime frame.");
                if (Time.frameCount <= batch.loadedFrame || EditorApplication.timeSinceStartup - batch.loadedAt < 0.1) return;
                if (PlaytestAutomation.IsRunning) throw new InvalidOperationException("Unexpected external run; batch will not adopt it.");
                string response = PlaytestAutomation.StartBatchCase(JsonUtility.ToJson(item.plan));
                if (!PlaytestAutomation.IsRunning) throw new InvalidOperationException("Case rejected: " + response);
                var child = JsonUtility.FromJson<PlaytestAutomation.RunResult>(PlaytestAutomation.Result());
                batch.childPath = child.resultPath; batch.childRunId = child.runId;
                batch.result.cases.Add(new CaseResult { name = item.name, scenePath = item.scenePath, state = "running", resultPath = child.resultPath, runId = child.runId });
                PlaytestAutomation.MarkIsolation("play-scene-single-reload", item.name);
                batch.phase = "run"; SaveBatch(batch.result);
            }
            else if (!PlaytestAutomation.IsRunning)
            {
                var child = JsonUtility.FromJson<PlaytestAutomation.RunResult>(File.ReadAllText(batch.childPath));
                if (child.runId != batch.childRunId) throw new InvalidOperationException("Child run identity mismatch.");
                CaseResult record = batch.result.cases[batch.result.cases.Count - 1]; record.state = child.state; record.levelVerdict = child.levelVerdict; record.reason = child.reason;
                batch.index++; SaveBatch(batch.result);
                if (batch.index == batch.result.plan.cases.Length) EndBatch("completed", "All cases attempted; inspect individual states/verdicts. No world rewind was claimed.");
                else { batch.phase = "load"; batch.childPath = null; batch.childRunId = null; }
            }
        }
        catch (Exception exception) { CancelOwnedChild("Batch coordinator error"); EndBatch("error", exception.Message); }
    }

    private static void CancelOwnedChild(string reason)
    {
        if (batch == null || !PlaytestAutomation.IsRunning || string.IsNullOrEmpty(batch.childRunId)) return;
        var current = JsonUtility.FromJson<PlaytestAutomation.RunResult>(PlaytestAutomation.Result());
        if (current.runId == batch.childRunId) PlaytestAutomation.Cancel(reason);
    }

    private static void EndBatch(string state, string reason)
    {
        if (batch == null) return;
        EditorApplication.update -= BatchTick;
        BatchSession ended = batch; batch = null;
        Application.runInBackground = ended.originalBackground;
        ended.result.state = state; ended.result.reason = reason;
        if (!string.IsNullOrEmpty(ended.childPath) && File.Exists(ended.childPath) && ended.result.cases.Count > 0)
        {
            var child = JsonUtility.FromJson<PlaytestAutomation.RunResult>(File.ReadAllText(ended.childPath));
            CaseResult record = ended.result.cases[ended.result.cases.Count - 1];
            if (child.runId == ended.childRunId) { record.state = child.state; record.levelVerdict = child.levelVerdict; record.reason = child.reason; }
        }
        for (int index = ended.result.cases.Count; index < ended.result.plan.cases.Length; index++)
        {
            BatchCase item = ended.result.plan.cases[index];
            ended.result.cases.Add(new CaseResult { name = item.name, scenePath = item.scenePath, state = "not_run", reason = reason });
        }
        ended.result.verdict = ended.result.cases.Any(item => item.state == "error") ? "error" :
            ended.result.cases.Any(item => item.state == "failed" || item.state == "timed_out" || item.levelVerdict == "failed") ? "failed" :
            state != "completed" || ended.result.cases.Any(item => item.state != "completed" || item.levelVerdict == "not_evaluated") ? "not_evaluated" :
            ended.result.cases.All(item => item.levelVerdict == "passed") ? "passed" : "observations_only";
        SessionState.SetString(BatchPathKey, ended.result.resultPath);
        SaveBatch(ended.result);
    }

    private static void SaveBatch(BatchResult result) => File.WriteAllText(result.resultPath, JsonUtility.ToJson(result, true));
    private static string WorkDirectory(string relative)
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".work"));
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", string.IsNullOrEmpty(relative) ? ".work/" + DateTime.Now.ToString("yyyyMMdd") + "-level-validation" : relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Batch output must be below project .work.");
        Directory.CreateDirectory(path); return path;
    }

    [Serializable]
    public sealed class MeasurementDifference
    {
        public string step;
        public string rightStep;
        public string role;
        public string pairing;
        public string leftVerdict;
        public string rightVerdict;
        public float displacementXDelta;
        public float heightDelta;
        public float maxSpeedDelta;
        public int sampleCountDelta;
    }
    [Serializable]
    public sealed class Comparison
    {
        public string leftRun;
        public string rightRun;
        public bool sameScene;
        public bool samePlan;
        public bool sameIsolation;
        public bool sameRecordedPhysics;
        public bool sameMovementSettings;
        public bool sameInitialBodies;
        public bool sameEnterPlayOptions;
        public string leftState;
        public string rightState;
        public string leftLevelVerdict;
        public string rightLevelVerdict;
        public string limit = "Descriptive differences only; different plans/conditions are not silently judged equivalent. Equal samples do not establish determinism or gameplay quality.";
        public List<MeasurementDifference> measurements = new List<MeasurementDifference>();
    }

    public static string Compare(string leftPath, string rightPath)
    {
        var left = JsonUtility.FromJson<PlaytestAutomation.RunResult>(File.ReadAllText(leftPath));
        var right = JsonUtility.FromJson<PlaytestAutomation.RunResult>(File.ReadAllText(rightPath));
        if (left == null || right == null || left.plan == null || right.plan == null) throw new ArgumentException("Comparison requires two actual run results.");
        var comparison = new Comparison { leftRun = left.runId, rightRun = right.runId, sameScene = left.scenePath == right.scenePath,
            samePlan = JsonUtility.ToJson(left.plan) == JsonUtility.ToJson(right.plan), sameIsolation = left.isolationMode == right.isolationMode,
            sameRecordedPhysics = left.fixedDeltaTime == right.fixedDeltaTime && left.gravity == right.gravity && left.initialTimeScale == right.initialTimeScale && left.inputUpdateMode == right.inputUpdateMode,
            leftState = left.state, rightState = right.state, leftLevelVerdict = left.levelVerdict, rightLevelVerdict = right.levelVerdict };
        comparison.sameMovementSettings = left.players != null && right.players != null && left.players.Length == right.players.Length && left.players.All(first => right.players.Any(second => first.role == second.role && first.settingsJson == second.settingsJson && first.scale == second.scale && first.colliderType == second.colliderType));
        comparison.sameInitialBodies = left.players != null && right.players != null && left.players.Length == right.players.Length && left.players.All(first => right.players.Any(second => first.role == second.role && JsonUtility.ToJson(first.initial) == JsonUtility.ToJson(second.initial)));
        comparison.sameEnterPlayOptions = left.enterPlayModeOptionsEnabled == right.enterPlayModeOptionsEnabled && left.enterPlayModeOptions == right.enterPlayModeOptions;
        for (int index = 0; index < Math.Max(left.measurements.Count, right.measurements.Count); index++)
        {
            PlaytestAutomation.Measurement first = index < left.measurements.Count ? left.measurements[index] : null;
            PlaytestAutomation.Measurement second = index < right.measurements.Count ? right.measurements[index] : null;
            bool paired = first != null && second != null && first.step == second.step && first.role == second.role;
            comparison.measurements.Add(new MeasurementDifference { step = first == null ? null : first.step, rightStep = second == null ? null : second.step,
                role = first == null ? second.role : first.role, pairing = paired ? "same-index-name-role" : "unmatched",
                leftVerdict = first == null ? "missing" : first.verdict, rightVerdict = second == null ? "missing" : second.verdict,
                displacementXDelta = paired ? second.displacement.x - first.displacement.x : 0, heightDelta = paired ? second.sampledHeight - first.sampledHeight : 0,
                maxSpeedDelta = paired ? second.maxAbsHorizontalSpeed - first.maxAbsHorizontalSpeed : 0, sampleCountDelta = paired ? second.samples - first.samples : 0 });
        }
        return JsonUtility.ToJson(comparison, true);
    }
}
