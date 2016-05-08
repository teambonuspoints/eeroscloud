using UnityEngine;
using System.Collections;

public static class ParticleFunctions{
	public static ParticleSystem setParticles(ParticleSystem psys, ParticleSystem.Particle[] particles){
		psys.SetParticles(particles, particles.Length);
		return psys;
	}

	public static ParticleSystem.Particle[] ParticleArray(int n){
		return new ParticleSystem.Particle[n];
	}
}