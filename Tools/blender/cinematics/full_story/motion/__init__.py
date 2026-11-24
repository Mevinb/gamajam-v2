"""Reusable offline motion and speech performance for PARASITE cinematics."""
import sys
sys.dont_write_bytecode = True
__all__ = ['MotionLibrary', 'SpeechPerformance']

def __getattr__(name):
    # Speech envelope playback is usable in ordinary Python without importing bpy.
    if name == 'MotionLibrary':
        from .library import MotionLibrary
        return MotionLibrary
    if name == 'SpeechPerformance':
        from .speech import SpeechPerformance
        return SpeechPerformance
    raise AttributeError(name)
