import { Injectable, signal } from '@angular/core';

export type ConnectionState =
  | 'connected'
  | 'connecting'
  | 'disconnected';

@Injectable({
  providedIn: 'root'
})
export class ConnectionStateService {
  private readonly stateSignal = signal<ConnectionState>('disconnected');

  readonly state = this.stateSignal.asReadonly();

  setConnecting(): void {
    this.stateSignal.set('connecting');
  }

  setConnected(): void {
    this.stateSignal.set('connected');
  }

  setDisconnected(): void {
    this.stateSignal.set('disconnected');
  }

  isConnected(): boolean {
    return this.stateSignal() === 'connected';
  }
}