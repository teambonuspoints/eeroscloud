(ns twa-modeller.core
  (:use arcadia.core
        arcadia.linear
        clojure.pprint
        clojure.repl)
  (:require [arcadia.introspection :as intro])
  (:import [UnityEngine
            GameObject
            Component
            Vector3
            ParticleSystem
            MeshRenderer
            MeshFilter
            Renderer
            Material
            Resources
            Transform
            Color
            Gizmos]
           ParticleFunctions))

(defmacro set-with! [obj [sym & props] & body]
  `(let [obj# ~obj
         ~sym (.. obj# ~@props)]
     (set! (.. obj# ~@props) (do ~@body))))

(defn mempr [& args]
  (pprint (apply intro/members args)))

;; ============================================================
;; stupid utils

(defn sel
  ([]
   (vec Selection/objects))
  ([& xs]
   (let [ar (into-array UnityEngine.Object xs)]
     (set! Selection/objects ar)
     (seq ar)))) ;; or something

(defn fsel []
  (first (sel)))

(defn asel
  ([]
   Selection/activeObject)
  ([x]
   (set! Selection/activeObject x)))

;; ============================================================

;; sort of a stupid function, should just take one obj
(defn assign-material [^Material mat, objs]
  (doseq [^Renderer r (keep #(cmpt % Renderer) objs)]
    (set! (.sharedMaterial r) mat)))

;; ============================================================

(defn sphere-point-array ^|UnityEngine.Vector3[]|
  ([n] (sphere-point-array n 1))
  ([n r]
   (let [^|UnityEngine.Vector3[]| ar (make-array Vector3 n)]
     (loop [i (int 0)]
       (if (< i n)
         (do
           (aset ar i (v3* (UnityEngine.Random/onUnitSphere) r))
           (recur (inc i)))
         ar)))))

(defn sphere-particle-array ^|UnityEngine.ParticleSystem+Particle[]|
  ([n] (sphere-particle-array n 1))
  ([n r]
   (let [^|UnityEngine.ParticleSystem+Particle[]| ar (make-array UnityEngine.ParticleSystem+Particle n)]
     (loop [i (int 0)]
       (if (< i n)
         (let [p (UnityEngine.ParticleSystem+Particle.)]
           (set! (.position p) (v3* (UnityEngine.Random/onUnitSphere) r))
           (aset ar i p)
           (recur (inc i)))
         ar)))))

(defn particles [psys n]
  (let [ar (make-array UnityEngine.ParticleSystem+Particle n)]
    (vec (take (.GetParticles psys ar) ar))))
