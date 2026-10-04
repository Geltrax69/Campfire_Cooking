using System.Linq;
using LivingWorld.Simulation.Agents;
using LivingWorld.Simulation.Core;
using LivingWorld.Simulation.Knowledge;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Knowledge
{
    /// <summary>
    /// Proves inter-village news travel (P6-03): news created from Millbrook's
    /// emergent events reaches neighbors after travel days, arrives late and
    /// sometimes distorted, moves destination mood, and shifts inter-village opinion.
    /// </summary>
    public sealed class NewsTravelTests
    {
        private static readonly VillageId Millbrook = new VillageId("village_millbrook");
        private static readonly VillageId KingsRest = new VillageId("village_kings_rest");
        private static readonly VillageId Oakhollow = new VillageId("village_oakhollow");
        private static readonly LocationId Square = new LocationId("loc_square");

        private static WorldState StateAtDay(long day, ulong seed = 20261004)
        {
            var state = new WorldState(seed, new GameTime((day - 1) * 1440));
            state.RestoreVillages(VillageFactory.CreateInitialVillages().Capture());
            return state;
        }

        private static void TickDays(WorldState state, NewsSystem system, int days)
        {
            for (int i = 0; i < days; i++)
            {
                state.Clock = state.Clock.Advance(1440);
                system.Tick(state);
            }
        }

        private static void AppendWolfAttack(WorldState state)
        {
            state.Events.Append(state.Clock, Square, WorldEventType.EmergentEventFired, quantity: 1);
        }

        [Test]
        public void TravelDaysViaMillbrook()
        {
            var state = StateAtDay(5);
            Assert.That(NewsSystem.TravelDays(state.Villages, Millbrook, KingsRest), Is.EqualTo(2));
            Assert.That(NewsSystem.TravelDays(state.Villages, KingsRest, Millbrook), Is.EqualTo(2));
            Assert.That(NewsSystem.TravelDays(state.Villages, Millbrook, Oakhollow), Is.EqualTo(1));
            Assert.That(NewsSystem.TravelDays(state.Villages, KingsRest, Oakhollow), Is.EqualTo(3));
            Assert.That(NewsSystem.TravelDays(state.Villages, Millbrook, Millbrook), Is.EqualTo(0));
        }

        [Test]
        public void NewsFromEmergentEventReachesNeighborsAfterTravelDays()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            AppendWolfAttack(state);

            system.Tick(state);

            var transit = state.News.GetInTransit();
            Assert.That(transit.Count, Is.EqualTo(2));
            NewsInTransit toKingsRest = transit.Single(t => t.To == KingsRest);
            NewsInTransit toOakhollow = transit.Single(t => t.To == Oakhollow);
            Assert.That(toKingsRest.News.Kind, Is.EqualTo(NewsKind.WolfAttack));
            Assert.That(toKingsRest.News.Origin, Is.EqualTo(Millbrook));
            Assert.That(toKingsRest.ArrivalDay, Is.EqualTo(7));
            Assert.That(toOakhollow.ArrivalDay, Is.EqualTo(6));
        }

        [Test]
        public void NewsTakesTime()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            // Direct publish with an explicit 3-day travel time (bypasses the route formula).
            state.News.Publish(Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60, Millbrook, KingsRest, 8);

            state.Clock = state.Clock.Advance(1440); // day 6
            system.Tick(state);
            Assert.That(state.News.GetArrivedFor(KingsRest), Is.Empty);

            state.Clock = state.Clock.Advance(2 * 1440); // day 8
            system.Tick(state);
            Assert.That(state.News.GetArrivedFor(KingsRest).Count, Is.EqualTo(1));
        }

        [Test]
        public void BadNewsLowersMood()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            int before = state.Villages[KingsRest].Mood;
            state.News.Publish(Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60, Millbrook, KingsRest, 5);

            system.Tick(state);

            Assert.That(state.Villages[KingsRest].Mood, Is.LessThan(before));
        }

        [Test]
        public void GoodNewsRaisesMood()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            int before = state.Villages[Oakhollow].Mood;
            state.News.Publish(Millbrook, Millbrook, NewsKind.Festival, 5, 40, Millbrook, Oakhollow, 5);

            system.Tick(state);

            Assert.That(state.Villages[Oakhollow].Mood, Is.GreaterThan(before));
        }

        [Test]
        public void NewsIsDistortedAboutTwentyPercentOfTheTime()
        {
            var state = StateAtDay(5, seed: 777);
            var system = new NewsSystem();
            const int count = 500;
            for (int i = 0; i < count; i++)
                state.News.Publish(Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60,
                    Millbrook, KingsRest, 5);

            system.Tick(state);

            int distorted = state.News.GetArrivedFor(KingsRest)
                .Count(a => a.News.Severity != 60);
            double fraction = (double)distorted / count;
            Assert.That(fraction, Is.InRange(0.10, 0.30),
                "Expected ~20% distortion, saw " + distorted + "/" + count);
        }

        [Test]
        public void DistortionKeepsSeverityInBounds()
        {
            var state = StateAtDay(5, seed: 1234);
            var system = new NewsSystem();
            // Extreme severities: distortion must clamp, never escape 0-100.
            for (int i = 0; i < 200; i++)
            {
                state.News.Publish(Millbrook, Millbrook, NewsKind.Fire, 5, 95,
                    Millbrook, KingsRest, 5);
                state.News.Publish(Millbrook, Millbrook, NewsKind.Festival, 5, 5,
                    Millbrook, Oakhollow, 5);
            }

            system.Tick(state);

            Assert.That(state.News.GetArrived().All(a =>
                a.News.Severity >= 0 && a.News.Severity <= 100), Is.True);
        }

        [Test]
        public void OpinionShiftsOnArrival()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            Assert.That(state.News.GetOpinion(Millbrook, KingsRest), Is.EqualTo(50));

            state.News.Publish(Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60,
                Millbrook, KingsRest, 5);
            system.Tick(state);
            Assert.That(state.News.GetOpinion(Millbrook, KingsRest), Is.LessThan(50));

            state.News.Publish(Millbrook, Millbrook, NewsKind.Festival, 5, 60,
                Millbrook, Oakhollow, 6);
            state.Clock = state.Clock.Advance(1440); // day 6
            system.Tick(state);
            Assert.That(state.News.GetOpinion(Millbrook, Oakhollow), Is.GreaterThan(50));
        }

        [Test]
        public void SystemRunsOncePerDay()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            AppendWolfAttack(state);

            system.Tick(state);
            system.Tick(state); // Same day: must not duplicate.

            Assert.That(state.News.GetInTransit().Count, Is.EqualTo(2));
        }

        [Test]
        public void FullWeekEndToEnd()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            AppendWolfAttack(state);
            system.Tick(state); // day 5: news created, in transit

            TickDays(state, system, 2); // days 6-7: both neighbors receive

            Assert.That(state.News.GetInTransit(), Is.Empty);
            Assert.That(state.News.GetArrivedFor(KingsRest).Count, Is.EqualTo(1));
            Assert.That(state.News.GetArrivedFor(Oakhollow).Count, Is.EqualTo(1));
            Assert.That(state.News.GetArrivedFor(KingsRest).Single().News.Kind,
                Is.EqualTo(NewsKind.WolfAttack));
        }

        [Test]
        public void CaptureRestoreRoundTrip()
        {
            var state = StateAtDay(5);
            var system = new NewsSystem();
            AppendWolfAttack(state);
            system.Tick(state);
            TickDays(state, system, 1); // day 6: Oakhollow receives
            state.News.ShiftOpinion(Millbrook, KingsRest, -3);

            NewsStoreSnapshot snapshot = state.News.Capture();
            var fresh = new NewsStore();
            fresh.Restore(snapshot);

            Assert.That(fresh.GetInTransit().Count, Is.EqualTo(1));
            Assert.That(fresh.GetArrivedFor(Oakhollow).Count, Is.EqualTo(1));
            Assert.That(fresh.GetOpinion(Millbrook, KingsRest), Is.EqualTo(47));
            Assert.That(fresh.LastDeliveryDay, Is.EqualTo(6));
        }

        [Test]
        public void PublishRejectsBadInput()
        {
            var state = StateAtDay(5);
            Assert.Throws<System.ArgumentException>(() => state.News.Publish(
                default, Millbrook, NewsKind.WolfAttack, 5, 60, Millbrook, KingsRest, 7));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.News.Publish(
                Millbrook, Millbrook, NewsKind.WolfAttack, 5, 101, Millbrook, KingsRest, 7));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => state.News.Publish(
                Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60, Millbrook, KingsRest, 4));
            Assert.Throws<System.ArgumentException>(() => state.News.Publish(
                Millbrook, Millbrook, NewsKind.WolfAttack, 5, 60, Millbrook, Millbrook, 5));
        }
    }
}
