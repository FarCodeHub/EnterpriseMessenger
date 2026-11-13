import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideTranslateService } from '@ngx-translate/core';

import { MessageBubbleComponent } from './message-bubble';
import { MessageDto } from '../../messages/models/message.dto';

describe('MessageBubbleComponent', () => {
  let component: MessageBubbleComponent;
  let fixture: ComponentFixture<MessageBubbleComponent>;

  const message: MessageDto = {
    id: '00000000-0000-0000-0000-000000000001',
    conversationId: '00000000-0000-0000-0000-000000000002',
    sequenceNumber: 1,
    senderUserId: '00000000-0000-0000-0000-000000000003',
    body: 'Hello',
    replyToMessageId: null,
    replyToSequenceNumber: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    editedAtUtc: null,
    deletedAtUtc: null,
    attachments: []
  };

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageBubbleComponent],
      providers: [provideTranslateService()],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageBubbleComponent);
    component = fixture.componentInstance;
    fixture.componentRef.setInput('message', message);
    fixture.componentRef.setInput('currentUserId', message.senderUserId);
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should treat messages from the current user as own messages', () => {
    expect(component.isOwnMessage()).toBe(true);
  });
});
