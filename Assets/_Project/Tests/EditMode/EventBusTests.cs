using System;
using HellpitRampage.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HellpitRampage.Tests
{
    public class EventBusTests
    {
        private struct TestEvent : IGameEvent { public int Value; }

        private GameObject _go;
        private EventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("EventBusTestHost");
            _bus = _go.AddComponent<EventBus>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
            _go = null;
            _bus = null;
        }

        [Test]
        public void Subscribe_HandlerReceivesPublishedEvent()
        {
            int captured = 0;
            Action<TestEvent> handler = e => captured = e.Value;
            _bus.Subscribe(handler);

            _bus.Publish(new TestEvent { Value = 42 });

            Assert.AreEqual(42, captured);
        }

        [Test]
        public void Unsubscribe_HandlerDoesNotReceiveAfterUnsubscribe()
        {
            bool fired = false;
            Action<TestEvent> handler = _ => fired = true;
            _bus.Subscribe(handler);
            _bus.Unsubscribe(handler);

            _bus.Publish(new TestEvent { Value = 1 });

            Assert.IsFalse(fired);
        }

        [Test]
        public void Publish_MultipleSubscribers_AllReceive()
        {
            int firedA = 0;
            int firedB = 0;
            _bus.Subscribe<TestEvent>(_ => firedA++);
            _bus.Subscribe<TestEvent>(_ => firedB++);

            _bus.Publish(new TestEvent { Value = 7 });

            Assert.AreEqual(1, firedA);
            Assert.AreEqual(1, firedB);
        }

        [Test]
        public void Publish_HandlerThrows_OtherHandlersStillReceive()
        {
            bool secondFired = false;
            _bus.Subscribe<TestEvent>(_ => throw new InvalidOperationException("boom"));
            _bus.Subscribe<TestEvent>(_ => secondFired = true);

            LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");
            _bus.Publish(new TestEvent { Value = 0 });

            Assert.IsTrue(secondFired);
        }
    }
}
