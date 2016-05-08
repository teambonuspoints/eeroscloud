(ns gaze-modeller.core
  (:use [source2016 core]
        [arcadia core linear hydrate]
        [clojure pprint repl])
  (:require [clojure.set :as set]
            [arcadia.introspection :as intro]
            [source2016.mesh :as m]
            [source2016.tims :as t :refer [set-with!]])
  (:import ;;OnDrawGizmosHook
   ;;FixedUpdateHook
   ;;RaycastHelper
   [System DateTime]
   [UnityEngine
    AudioClip
    AudioSource
    MeshCollider
    Time
    Color
    PhysicMaterial
    Transform
    BoxCollider
    ForceMode
    Rigidbody
    Time
    Collider
    Mesh
    CombineInstance
    PhysicMaterialCombine]
   [UnityEngine
    MeshFilter MeshRenderer Shader
    Material GameObject Component
    Color]
   [UnityEditor
    Selection]
   [UnityEngine
    Quaternion Vector2 Vector3 Transform GameObject Component
    Debug MeshFilter Mesh MeshRenderer Color
    LineRenderer Material Shader
    Gizmos Texture2D Resources Mathf
    Physics Ray RaycastHit
    Input
    Camera
    Application]))

;; doesn't like this in ns form for some reason
(import
  GazeMeshModellerFunctions
  OnDrawGizmosHook
  UpdateHook
  FixedUpdateHook)

(defn destroy-named [name]
  (when-let [g (object-named name)]
    (try
      (UnityEngine.Object/Destroy g)
      (catch System.InvalidCastException e
        (throw (Exception. (str "type of name: " (type name) "; type of g: " (type g))))))))

;; ============================================================
;; raycasting

(defn raycast [^Ray ray]
  (when-let [^RaycastHit hit (first (RayCastHelper/raycast ray))]
    {:collider (.collider hit)
     :point (.point hit)
     :transform (.transform hit)}))

(defn raycast* [^Ray ray]
  (first (RayCastHelper/raycast ray)))

(defn raycast-plane [^Plane plane, ^Ray ray]
  (first (RayCastHelper/raycastPlane plane ray)))

(defn forward-ray ^Ray [x]
  (let [^Transform t (cmpt x Transform)]
    (Ray. (.position t) (.forward t))))

(defn raycast-forward [x]
  (raycast (forward-ray x)))

;; ============================================================
;; scene

(def player-1
  (object-named "player_1"))

(def cube-spec
  (t/kill! (create-primitive :cube)))

(def sphere-spec
  (t/kill! (create-primitive :sphere)))

(def cylinder-spec
  (t/kill! (create-primitive :cylinder)))

(comment
  (defscn player-1-gaze
    (with-cmpt player-1 [p1tr Transform]
      (let [length 2
            rot (aa 90 1 0 0)
            ball (-> sphere-spec
                   (deep-merge-mv
                     {:transform [{:local-position (qv* rot (v3 0 length  0))
                                   :local-scale (v3 0.2)}]
                      :name "ball"}))
            cylinder (-> cylinder-spec
                       (deep-merge-mv
                         {:transform [{:local-position (qv* rot
                                                         (v3*
                                                           (v3 0 (/ 1 2) 0)
                                                           length))
                                       :local-rotation rot
                                       :local-scale (v3scale
                                                      (v3 0.1 0.5 0.1)
                                                      (v3 1 length 1))}]
                          :name "reach-cylinder"}))
            p1g (hydrate {:name "player-1-gaze"
                          :children [cylinder
                                     ball]})]
        (with-cmpt p1g [tr Transform]
          (.SetParent tr p1tr false))
        p1g))))

;; pop all this nonsense out into something else when time stuff is known

(defn scale-mesh [^Mesh m scl]
  (let [m2 (m/clone-mesh m)
        ^|UnityEngine.Vector3[]| vs (.vertices m2)
        n (int (count vs))]
    (loop [i (int 0)]
      (when (< i n)
        (aset vs i (v3* (aget vs i) scl))
        (recur (inc i))))
    (set! (.vertices m2) vs)
    m2))

(defscn model-sphere
  (destroy-named "model-sphere")
  (let [ms (-> sphere-spec
             (deep-merge-mv
               {:name "model-sphere"
                :transform [{:local-position (v3 0 2 0)
                             :local-scale (v3 0.006)}]})
             (dissoc :sphere-collider)
             hydrate)
        mesho ;;(.mesh (.GetComponent ms MeshFilter))
        (Resources/Load "models/1929-Cunningham" Mesh)]
    (.AddComponent ms MeshCollider)
    (with-cmpt ms [mc MeshCollider
                   mf MeshFilter]
      (set! (. mc sharedMesh) mesho)
      (set! (. mf sharedMesh) mesho))
    ;; (set! (. mc sharedMesh)
    ;;   (. (.GetComponent ms MeshFilter) mesh))
    ms))

;; ============================================================
;; behavior

(def gaze-update-log (atom nil))

(defscn gazer
  (destroy-named "gazer")
  (let [gzr (hydrate
              (-> sphere-spec
                (deep-merge-mv 
                  {:name "gazer"
                   :transform [{:local-position (v3 0 0 1)
                                :local-scale (v3 0.2)}]})))]
    (.SetParent (.transform gzr) (.transform player-1) false)
    gzr))

(defn gaze-update [obj]
  (with-cmpt obj [tr Transform]
    (if-let [rc (raycast* (forward-ray obj))]
      (let [point (.point rc)]
        (do
          (reset! gaze-update-log rc)
          (set! Gizmos/color Color/red)
          (Gizmos/DrawSphere point, 0.2)
          (Gizmos/DrawLine (.. obj transform position) point))))))

;; ============================================================

(defn attach-raycast-gizmo [obj]
  (hook! obj OnDrawGizmosHook
    #'gaze-update))

;; ============================================================
;; distortion

(defn mesh-bloat [^Mesh m, ^Vector3 bloat-point, strength]
  (let [m2 (m/clone-mesh m)
        ^|UnityEngine.Vector3[]| vs (.vertices m2)
        n (int (count vs))
        ;;^|UnityEngine.Vector3[]| vs2 (make-array Vector3 n)
        ]
    (loop [i (int 0)]
      (if (< i n)
        (let [p (aget vs i)
              diff (v3- bloat-point p)
              mag (.magnitude diff)]
          (when true ;;(< mag 1)
            (aset vs i
              (v3- p
                (v3* (.normalized diff)
                  (/ ;; cheating
                    (min ;;10
                      (Mathf/Pow (* strength mag)
                        (- 0.01)
                        ;;(- 5)
                        ))
                    1)))))
          (recur (inc i)))))
    (set! (.vertices m2) vs)
    m2))

(defn smoother-step [x]
  (let [a (Mathf/Clamp01 x)]
    (+ (* 6 (Mathf/Pow a 5))
      (- (* 15 (Mathf/Pow a 4)))
      (* 10 (Mathf/Pow a 3)))))

(defn smoother-loop [x, spread-radius, damp-radius]
  (if (< x damp-radius)
    (smoother-step (/ (- damp-radius x) damp-radius))
    (smoother-step (/ (- x damp-radius) (- spread-radius damp-radius)))))

(defn mesh-bloat-2
  ([^Mesh m, ^Vector3 bloat-point, strength]
   (mesh-bloat-2 m, bloat-point, strength, 1))
  ([^Mesh m, ^Vector3 bloat-point, strength, radius]
   (let [m2 (m/clone-mesh m)
         ^|UnityEngine.Vector3[]| vs (.vertices m2)
         n (int (count vs))
         ;;^|UnityEngine.Vector3[]| vs2 (make-array Vector3 n)
         ]
     (loop [i (int 0)]
       (if (< i n)
         (let [p (aget vs i)
               diff (v3- p bloat-point)
               mag (.magnitude diff)]
           (when (< mag radius)
             (let [delta (* strength
                           (- 1 ;; flip it
                             (smoother-step (/ mag radius))))
                   deltav (v3* (.normalized diff) delta)
                   p2 (v3+ p deltav)]
               (if (and (< delta 0) (< mag (.magnitude (v3- p p2))))
                 (aset vs i bloat-point)
                 (aset vs i p2))))
           (recur (inc i)))))
     (set! (.vertices m2) vs)
     m2)))

(defn model-strike [target-object hit-point]
  (with-cmpt target-object [mf MeshFilter
                            mc MeshCollider
                            tr Transform]
    (let [m2 (mesh-bloat (.. mf mesh)
               (.InverseTransformPoint tr hit-point)
               2)]
      (set! (. mf sharedMesh) m2)
      (set! (. mc sharedMesh) m2)))
  target-object)

;; ============================================================
;; sound

(defn load-clip [name]
  (Resources/Load (str "sounds/" name) AudioClip))

(defn move-to-global [obj pt]
  (set! (.. obj transform position) pt)
  obj)

(defn find-asource-obj [obj]
  (.Find (.transform obj) "AudioSource"))

(defn ensure-clip [string-or-clip]
  (cond
    (instance? AudioClip string-or-clip) string-or-clip
    (string? string-or-clip) (load-clip string-or-clip)
    :else (throw (Exception.
                   (str "Invalid type for string-or-clip; type: "
                     (class string-or-clip))))))

(defn audio-source-play-clip
  ([audio-source clip]
   (audio-source-play-clip audio-source clip false))
  ([audio-source clip loop?]
   (assert (instance? AudioSource audio-source))
   (let [clip (ensure-clip clip)]
     (set! (.clip audio-source) clip)
     (set! (.loop audio-source) loop?)
     (set! (.spatialBlend audio-source) 1)
     (.Play audio-source)
     audio-source)))

(defn play-clip-at-pt [obj pt clip loop?]
  (let [audio-source-obj (find-asource-obj obj)]
    (move-to-global audio-source-obj pt)
    (audio-source-play-clip
      (.GetComponent audio-source-obj AudioSource)
      clip
      loop?)))

(defn play-clip-at-hit [hit clip loop?]
  (assert (.collider hit))
  (play-clip-at-pt
    (.. hit transform gameObject)
    (.. hit point)
    clip
    loop?))

;; ============================================================
;; frips and frups

(defn scene-view ^UnityEditor.SceneView []
  (or
    UnityEditor.SceneView/currentDrawingSceneView
    (first (UnityEditor.SceneView/sceneViews))))

(defn align-to-scene-view [x]
  (when-let [^UnityEditor.SceneView sv (scene-view)]
    (let [tr (cmpt x Transform)]
      (doto tr
        (set-with! [_ position]
          (.pivot sv))
        (set-with! [_ rotation]
          (.rotation sv))))))

(def cam
  (gobj (object-typed UnityEngine.Camera)))

(defn cam-to-scene []
  (align-to-scene-view cam))

;; ============================================================
;; benchmarking

(definline fine-millis []
  `(/ (.Ticks System.DateTime/Now) 1e4))

(defmacro time-it [n & body]
  `(let [t1# (fine-millis)]
     (dotimes [_# ~n]
       ~@body)
     (/ (- (fine-millis) t1#) ~n)))

;; ============================================================
;; GO

(defn construct []
  (attach-raycast-gizmo gazer))



