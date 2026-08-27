using FluentAssertions;
using SeniorConnect.Modules.Community.Domain;
using Xunit;

namespace SeniorConnect.Modules.Community.Tests;

public sealed class ContextualDiscussionTests
{
    [Fact]
    public void Thread_can_be_created_for_group_or_event_context()
    {
        var groupId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var thread = MessageThread.Create(ThreadContextType.Group, groupId, "Spaziergang Vorbereitung", creatorId);

        thread.ContextType.Should().Be(ThreadContextType.Group);
        thread.ContextId.Should().Be(groupId);
        thread.Title.Should().Be("Spaziergang Vorbereitung");
        thread.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void Thread_message_creation_validates_non_empty_content()
    {
        var threadId = Guid.NewGuid();
        var senderId = Guid.NewGuid();

        var validMsgResult = ThreadMessage.Create(threadId, senderId, "Hallo zusammen, wer bringt die Karten mit?");
        validMsgResult.IsSuccess.Should().BeTrue();
        var msg = validMsgResult.Value!;
        msg.Content.Should().Be("Hallo zusammen, wer bringt die Karten mit?");
        msg.ThreadId.Should().Be(threadId);
        msg.SenderUserId.Should().Be(senderId);

        var emptyMsgResult = ThreadMessage.Create(threadId, senderId, "   ");
        emptyMsgResult.IsFailure.Should().BeTrue();
        emptyMsgResult.Error!.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Thread_can_be_closed_by_admin()
    {
        var thread = MessageThread.Create(ThreadContextType.Event, Guid.NewGuid(), "Event Thread", Guid.NewGuid());
        var adminId = Guid.NewGuid();

        thread.Close(adminId);
        thread.IsClosed.Should().BeTrue();
        thread.UpdatedBy.Should().Be(adminId);
    }
}
