import { Component, OnInit, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs/Subscription';
import { Observable } from 'rxjs/Observable';
import { Store } from '@ngrx/store';
import { DirectMessagesState } from '../store/directmessages.state';
import * as directMessagesAction from '../store/directmessages.action';
import { OidcSecurityService } from 'angular-auth-oidc-client';
import { OnlineUser } from '../models/online-user';
import { DirectMessage } from '../models/direct-message';

@Component({
    selector: 'app-direct-message-component',
    templateUrl: './direct-message.component.html'
})

export class DirectMessagesComponent implements OnInit, OnDestroy {
    public async: any;
    onlineUsers: OnlineUser[];
    onlineUser: OnlineUser;
    directMessages: DirectMessage[];
    message = 'message';
    dmState$: Observable<DirectMessagesState>;

    isAuthorizedSubscription: Subscription;
    isAuthorized: boolean;

    constructor(
        private store: Store<any>,
        private oidcSecurityService: OidcSecurityService
    ) {
        this.dmState$ = this.store.select<DirectMessagesState>(state => state.dm.dm);
    }

    public send(): void {
        this.store.dispatch(new directMessagesAction.SendDirectMessageAction(this.message, this.onlineUser.userName));
    }

    ngOnInit() {
        this.isAuthorizedSubscription = this.oidcSecurityService.getIsAuthorized().subscribe(
            (isAuthorized: boolean) => {
                this.isAuthorized = isAuthorized;
                if (this.isAuthorized) {
                }
            });
        console.log('IsAuthorized:' + this.isAuthorized);
    }

    ngOnDestroy(): void {
        this.isAuthorizedSubscription.unsubscribe();
    }
}
