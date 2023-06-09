import { Subscription, Observable } from 'rxjs';

import { HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';

import { HubConnection } from '@microsoft/signalr';
import { Store } from '@ngrx/store';
import * as directMessagesActions from './store/directmessages.action';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { OnlineUser } from './models/online-user';
import * as signalR from '@microsoft/signalr';

@Injectable()
export class DirectMessagesService {
  private hubConnection: HubConnection | undefined;
  private headers: HttpHeaders | undefined;
  private token: string = '';

  isAuthorizedSubscription: Subscription | undefined;
  isAuthorized = false;

  constructor(
    private store: Store<any>,
    private oidcSecurityService: OidcSecurityService
  ) {
    this.headers = new HttpHeaders();
    this.headers = this.headers.set('Content-Type', 'application/json');
    this.headers = this.headers.set('Accept', 'application/json');

    this.init();
  }

  sendDirectMessage(message: string, userId: string): string {
    if (this.hubConnection) {
      this.hubConnection.invoke('SendDirectMessage', message, userId);
    }
    return message;
  }

  leave(): void {
    if (this.hubConnection) {
      this.hubConnection.invoke('Leave');
    }
  }

  join(): void {
    console.log('DMS: send join');
    if (this.hubConnection) {
      this.hubConnection.invoke('Join');
    }
  }

  private init(): void {

    this.oidcSecurityService.isAuthenticated$.subscribe(({ isAuthenticated }) => {
      this.isAuthorized = isAuthenticated;

      if (this.isAuthorized) {
        this.initHub();
      }

      console.warn('authenticated: ', isAuthenticated);
    });
  }

  private initHub(): void {
    console.log('DMS: initHub');

    this.oidcSecurityService.getAccessToken().subscribe((token) => {

      let tokenValue = '';
      this.token = token;
      const tokenApiHeader = 'Bearer ' + this.token;
      this.headers = this.headers.append('Authorization', tokenApiHeader);
      console.log(tokenApiHeader)
      tokenValue = '?token=' + token;


      const url = 'https://localhost:44390/';

      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(`${url}usersdm${tokenValue}`)
        .configureLogging(signalR.LogLevel.Information)
        .build();

      this.hubConnection.start().catch((err) => console.error(err.toString()));

      this.hubConnection.on('NewOnlineUser', (onlineUser: OnlineUser) => {
        console.log('DMS: NewOnlineUser received');
        console.log(onlineUser);
        this.store.dispatch(
          directMessagesActions.receivedNewOnlineUserAction({
            payload: onlineUser,
          })
        );
      });

      this.hubConnection.on('OnlineUsers', (onlineUsers: OnlineUser[]) => {
        console.log('DMS: OnlineUsers received');
        console.log(onlineUsers);
        this.store.dispatch(
          directMessagesActions.receivedOnlineUsersAction({
            payload: onlineUsers,
          })
        );
      });

      this.hubConnection.on('Joined', (onlineUser: OnlineUser) => {
        console.log('DMS: Joined received');
        console.log(onlineUser);
      });

      this.hubConnection.on(
        'SendDM',
        (message: string, onlineUser: OnlineUser) => {
          console.log('DMS: SendDM received');
          this.store.dispatch(
            directMessagesActions.receivedDirectMessageForUserAction({
              payload: { onlineUser, message },
            })
          );
        }
      );

      this.hubConnection.on('UserLeft', (name: string) => {
        console.log('DMS: UserLeft received');
        this.store.dispatch(
          directMessagesActions.receivedUserLeftAction({ payload: name })
        );
      });
    });
  }
}
