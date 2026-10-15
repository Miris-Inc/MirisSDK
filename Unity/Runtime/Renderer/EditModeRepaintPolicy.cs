// Copyright © 2026 Miris, Inc. All rights reserved.

using System;

namespace Miris.Runtime
{
    // Decides when the editor should tick the player loop and repaint while Shark draws in edit
    // mode. Edit mode only ticks when something in the editor changes, but Shark is brought up,
    // adopts its client and uploads streamed data only as it renders - so without this a scene
    // stops streaming the moment the user stops touching it, and with it unbounded the editor
    // never goes idle.
    //
    // Times are in seconds on any monotonic clock.
    internal sealed class EditModeRepaintPolicy
    {
        // How long after the last change the editor keeps ticking. Streaming refines in batches, and
        // the gaps between them are short while it is under way.
        internal const double SettleSeconds = 3.0;

        // How long waiting keeps the editor ticking with nothing changing, so a stream that will
        // never load cannot keep it busy forever.
        internal const double WaitingLimitSeconds = 30.0;

        // About 30 Hz: enough to watch content arrive without loading the editor.
        internal const double MinIntervalSeconds = 1.0 / 30.0;

        double m_lastActivity = double.NegativeInfinity;
        double m_lastRepaint = double.NegativeInfinity;
        double m_waitingSince = double.NaN;

        // Something changed what the cameras should show.
        internal void NoteActivity(double now)
        {
            m_lastActivity = now;
        }

        // `waiting`: something that only progresses on further frames has not finished.
        internal bool ShouldRepaint(double now, bool waiting)
        {
            if (!waiting)
            {
                m_waitingSince = double.NaN;
            }
            else if (double.IsNaN(m_waitingSince))
            {
                m_waitingSince = now;
            }

            if (now - m_lastRepaint < MinIntervalSeconds)
            {
                return false;
            }
            bool settling = now - m_lastActivity < SettleSeconds;
            bool stillWaiting = waiting && now - Math.Max(m_waitingSince, m_lastActivity) < WaitingLimitSeconds;
            if (!settling && !stillWaiting)
            {
                return false;
            }
            m_lastRepaint = now;
            return true;
        }
    }
}
