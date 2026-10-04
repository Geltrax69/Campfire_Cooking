using NUnit.Framework;
using UnityEngine;
namespace LivingWorld.Game.Player.Tests
{
    public sealed class TownWalkerTests
    {
        [Test] public void ForwardFollowsCameraWithoutVerticalMovement()
        {
            var direction = TownWalker.MovementDirection(Vector2.up, new Vector3(1, -1, 0));
            Assert.That(direction.x, Is.EqualTo(1).Within(0.001)); Assert.That(direction.y, Is.Zero);
        }
        [Test] public void DiagonalInputCannotMoveFasterThanForward()
        {
            Assert.That(TownWalker.MovementDirection(Vector2.one, Vector3.forward).magnitude, Is.EqualTo(1).Within(0.001));
        }
        [Test] public void ZeroInputDoesNotMoveAndVerticalCameraHasSafeFallback()
        {
            Assert.That(TownWalker.MovementDirection(Vector2.zero, Vector3.up), Is.EqualTo(Vector3.zero));
            Assert.That(TownWalker.MovementDirection(Vector2.up, Vector3.up), Is.EqualTo(Vector3.forward));
        }
    }
}
