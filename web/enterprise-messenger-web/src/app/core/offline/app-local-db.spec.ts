import 'fake-indexeddb/auto';

import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import Dexie from 'dexie';
import { of } from 'rxjs';

import { AuthApiService } from '../auth/auth-api.service';
import { AuthConfigService } from '../auth/auth-config.service';
import { AuthService } from '../auth/auth.service';
import { OidcService } from '../auth/oidc.service';
import { MessageDto } from '../../features/messages/models/message.dto';
import { deleteLocalDbFor, localDb, useLocalDbFor } from './app-local-db';

// The browser may be shared: one user's cached messages and unsent changes must never reach another user.
describe('local database per user', () => {
  const message = (id: string): MessageDto =>
    ({ id, conversationId: 'c1', sequenceNumber: 1, body: 'secret', createdAtUtc: new Date().toISOString() }) as MessageDto;

  afterEach(async () => {
    useLocalDbFor(null);
    await Promise.all((await Dexie.getDatabaseNames()).map(name => Dexie.delete(name)));
  });

  it('keeps each user in their own database', async () => {
    useLocalDbFor('alice');
    await localDb().messages.put(message('m1'));
    await localDb().pendingOperations.put({
      id: 'op1', type: 'sendMessage', payload: {}, createdAtUtc: '', retryCount: 0, status: 'pending'
    });

    useLocalDbFor('bob');
    expect(await localDb().messages.count()).toBe(0);
    expect(await localDb().pendingOperations.count()).toBe(0);

    useLocalDbFor('alice');
    expect(await localDb().messages.count()).toBe(1);
  });

  it('deletes a user\'s data on sign-out and switches to the empty anonymous database', async () => {
    useLocalDbFor('alice');
    await localDb().messages.put(message('m1'));

    await deleteLocalDbFor('alice');

    expect(localDb().name).toBe('EnterpriseMessengerDb-anonymous');
    useLocalDbFor('alice');
    expect(await localDb().messages.count()).toBe(0);
  });

  it('signs in into the user\'s database and deletes it on logout', async () => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        { provide: HttpClient, useValue: {} },
        { provide: AuthConfigService, useValue: {} },
        { provide: OidcService, useValue: {} },
        {
          provide: AuthApiService,
          useValue: {
            login: () => of({
              accessToken: 'token',
              refreshToken: 'refresh',
              tokenType: 'Bearer',
              expiresInSeconds: 3600,
              user: { id: 'alice', displayName: 'Alice', userName: 'alice' }
            }),
            logout: () => of({})
          }
        }
      ]
    });

    const auth = TestBed.inject(AuthService);

    await new Promise<void>(resolve => auth.login('alice').subscribe({ complete: resolve }));
    expect(localDb().name).toBe('EnterpriseMessengerDb-alice');
    await localDb().messages.put(message('m1'));

    await new Promise<void>(resolve => auth.logout().subscribe({ complete: resolve }));

    expect(localDb().name).toBe('EnterpriseMessengerDb-anonymous');
    expect(await Dexie.getDatabaseNames()).not.toContain('EnterpriseMessengerDb-alice');
  });
});
