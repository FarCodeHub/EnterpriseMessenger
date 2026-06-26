import { ComponentFixture, TestBed } from '@angular/core/testing';

import { MessageActionMenu } from './message-action-menu';

describe('MessageActionMenu', () => {
  let component: MessageActionMenu;
  let fixture: ComponentFixture<MessageActionMenu>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [MessageActionMenu],
    }).compileComponents();

    fixture = TestBed.createComponent(MessageActionMenu);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
