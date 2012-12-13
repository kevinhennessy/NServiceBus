﻿namespace NServiceBus.RabbitMQ.Tests
{
    using System;
    using System.Text;
    using NUnit.Framework;
    using Unicast.Queuing;
    using global::RabbitMQ.Client;
    using global::RabbitMQ.Client.Events;
    using global::RabbitMQ.Client.Exceptions;


    [TestFixture, Explicit("Integration tests")]
    public class Sending
    {
        const string TESTQUEUE = "testendpoint";

        [Test]
        public void Should_populate_the_body()
        {
            var body = Encoding.UTF8.GetBytes("<TestMessage/>");

            Verify(new TransportMessageBuilder().WithBody(body),
                 received => Assert.AreEqual(body, received.Body));
        }


        [Test]
        public void Should_set_the_content_type()
        {
            VerifyRabbit(new TransportMessageBuilder().WithHeader(Headers.ContentType, "application/json"),
                received => Assert.AreEqual("application/json", received.BasicProperties.ContentType ));
 
        }

        [Test]
        public void Should_set_the_time_to_be_received()
        {

            var timeToBeReceived = TimeSpan.FromDays(1);


            VerifyRabbit(new TransportMessageBuilder().TimeToBeReceived(timeToBeReceived),
                received => Assert.AreEqual(timeToBeReceived.TotalMilliseconds.ToString(), received.BasicProperties.Expiration));

        }


        [Test]
        public void Should_set_the_reply_to_address()
        {
            var address = Address.Parse("myAddress");

            Verify(new TransportMessageBuilder().ReplyToAddress(address),
                (t, r) =>
                    {
                        Assert.AreEqual(address, t.ReplyToAddress);
                        Assert.AreEqual(address.Queue, r.BasicProperties.ReplyTo);
                    });

        }


        [Test, Ignore("Not sure we should enforce this")]
        public void Should_throw_when_sending_to_a_nonexisting_queue()
        {
            Assert.Throws<QueueNotFoundException>(() =>
                 sender.Send(new TransportMessage
                 {

                 }, Address.Parse("NonExistingQueue@localhost")));
        }


        void Verify(TransportMessageBuilder builder, Action<TransportMessage, BasicDeliverEventArgs> assertion)
        {
            var message = builder.Build();

            SendMessage(message);

            var result = Consume(message.Id);

            assertion(result.ToTransportMessage(), result);
        }
        void Verify(TransportMessageBuilder builder, Action< TransportMessage> assertion)
        {
            Verify(builder,(t,r)=>assertion(t));           
        }

        void VerifyRabbit(TransportMessageBuilder builder, Action<BasicDeliverEventArgs> assertion)
        {
            Verify(builder, (t, r) => assertion(r));
        }


        
        void SendMessage(TransportMessage message)
        {
            MakeSureQueueExists(connection);
            sender.Send(message, Address.Parse("TestEndpoint@localhost"));
        }

        BasicDeliverEventArgs Consume(string id)
        {

            using (var channel = connection.CreateModel())
            {
                var consumer = new QueueingBasicConsumer(channel);

                channel.BasicConsume(TESTQUEUE, true, consumer);

                object message;

                if (!consumer.Queue.Dequeue(1000, out message))
                    throw new InvalidOperationException("No message found in queue");

                var e = (BasicDeliverEventArgs)message;

                if (e.BasicProperties.MessageId != id)
                    throw new InvalidOperationException("Unexpected message found in queue");

                return e;
            }
        }



        static void MakeSureQueueExists(IConnection connection)
        {
            using (var channel = connection.CreateModel())
            {
                channel.QueueDeclare(TESTQUEUE, true, false, false, null);
                channel.QueuePurge(TESTQUEUE);
            }

        }




        [SetUp]
        public void SetUp()
        {
            factory = new ConnectionFactory { HostName = "localhost" };
            connection = factory.CreateConnection();
            sender = new RabbitMqMessageSender { Connection = connection };
        }

        [TearDown]
        public void TearDown()
        {
            connection.Close();
            connection.Dispose();
        }
        ConnectionFactory factory;
        IConnection connection;
        RabbitMqMessageSender sender;

    }
}