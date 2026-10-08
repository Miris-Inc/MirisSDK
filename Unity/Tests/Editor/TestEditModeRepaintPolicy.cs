// Copyright © 2026 Miris, Inc. All rights reserved.

using Miris.Runtime;
using NUnit.Framework;

namespace Miris.Tests.Editor
{
    public class TestEditModeRepaintPolicy
    {
        const double Interval = EditModeRepaintPolicy.MinIntervalSeconds;
        const double Settle = EditModeRepaintPolicy.SettleSeconds;

        [Test]
        public void IdleEditorIsLeftAlone()
        {
            EditModeRepaintPolicy policy = new();
            Assert.IsFalse(policy.ShouldRepaint(100.0, waiting: false));
        }

        [Test]
        public void WaitingKeepsTickingAtTheThrottledRate()
        {
            EditModeRepaintPolicy policy = new();
            Assert.IsTrue(policy.ShouldRepaint(100.0, waiting: true));
            Assert.IsFalse(policy.ShouldRepaint(100.0 + Interval * 0.5, waiting: true));
            Assert.IsTrue(policy.ShouldRepaint(100.0 + Interval * 1.5, waiting: true));
        }

        [Test]
        public void ActivityKeepsTickingUntilItSettles()
        {
            EditModeRepaintPolicy policy = new();
            policy.NoteActivity(100.0);
            Assert.IsTrue(policy.ShouldRepaint(100.0, waiting: false));
            Assert.IsTrue(policy.ShouldRepaint(100.0 + Settle * 0.5, waiting: false));
            Assert.IsFalse(policy.ShouldRepaint(100.0 + Settle, waiting: false));
        }

        [Test]
        public void WaitingWithNothingChangingGivesUp()
        {
            const double Limit = EditModeRepaintPolicy.WaitingLimitSeconds;
            EditModeRepaintPolicy policy = new();
            Assert.IsTrue(policy.ShouldRepaint(100.0, waiting: true));
            Assert.IsTrue(policy.ShouldRepaint(100.0 + Limit * 0.9, waiting: true));
            Assert.IsFalse(policy.ShouldRepaint(100.0 + Limit, waiting: true));

            // Progress while still waiting buys it more time.
            policy.NoteActivity(100.0 + Limit * 1.5);
            Assert.IsTrue(policy.ShouldRepaint(100.0 + Limit * 1.5 + Settle * 2.0, waiting: true));
        }

        [Test]
        public void FreshActivityRestartsTheSettlePeriod()
        {
            EditModeRepaintPolicy policy = new();
            policy.NoteActivity(100.0);
            policy.NoteActivity(100.0 + Settle * 0.9);
            Assert.IsTrue(policy.ShouldRepaint(100.0 + Settle * 1.5, waiting: false));
            Assert.IsFalse(policy.ShouldRepaint(100.0 + Settle * 2.0, waiting: false));
        }
    }
}
