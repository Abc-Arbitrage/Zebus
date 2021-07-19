﻿using System;
using System.Threading.Tasks;
using Abc.Zebus.Testing;
using Abc.Zebus.Tests.Messages;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Testing
{
    [TestFixture]
    public class TestBusTests
    {
        [Test]
        public void should_notify_explicit_handlers()
        {
            // Arrange
            var bus = new TestBus();

            var received = false;
            bus.Subscribe<FakeEvent>(e => received = true);

            // Act
            bus.Publish(new FakeEvent(1));

            // Assert
            Assert.That(received);
        }

        [Test]
        public async Task should_handle_exception_with_async_executor()
        {
            // Arrange
            var bus = new TestBus { HandlerExecutor = new TestBus.AsyncHandlerExecutor() };
            var exception = new Exception("Expected");
            bus.AddHandlerThatThrows<FakeCommand>(exception);

            // Act
            var result = await bus.Send(new FakeCommand(123));

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ResponseMessage, Is.EqualTo(exception.Message));
        }

        [Test]
        public async Task should_handle_message_processing_exception_with_async_executor()
        {
            // Arrange
            var bus = new TestBus { HandlerExecutor = new TestBus.AsyncHandlerExecutor() };
            var exception = new MessageProcessingException("Expected") { ErrorCode = 999 };
            bus.AddHandlerThatThrows<FakeCommand>(exception);

            // Act
            var result = await bus.Send(new FakeCommand(123));

            // Assert
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo(exception.ErrorCode));
            Assert.That(result.ResponseMessage, Is.EqualTo(exception.Message));
        }
    }
}
