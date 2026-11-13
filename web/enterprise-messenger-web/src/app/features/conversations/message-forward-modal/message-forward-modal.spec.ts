import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MessageForwardModalComponent } from './message-forward-modal';

describe('MessageForwardModalComponent', () => {
  let component: MessageForwardModalComponent;
  let fixture: ComponentFixture<MessageForwardModalComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageForwardModalComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageForwardModalComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
