import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MessageForwardModal } from './message-forward-modal';

describe('MessageForwardModal', () => {
  let component: MessageForwardModal;
  let fixture: ComponentFixture<MessageForwardModal>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageForwardModal],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageForwardModal);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
