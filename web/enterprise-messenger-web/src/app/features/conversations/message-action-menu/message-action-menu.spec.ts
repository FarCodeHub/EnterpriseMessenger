import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MessageActionMenuComponent } from './message-action-menu';

describe('MessageActionMenuComponent', () => {
  let component: MessageActionMenuComponent;
  let fixture: ComponentFixture<MessageActionMenuComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageActionMenuComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageActionMenuComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
