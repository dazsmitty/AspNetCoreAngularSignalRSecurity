﻿using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ApiServer.SignalRHubs;

[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public class UsersDmHub : Hub
{
    private readonly UserInfoInMemory _userInfoInMemory;

    public UsersDmHub(UserInfoInMemory userInfoInMemory)
    {
        _userInfoInMemory = userInfoInMemory;
    }

    public async Task Leave()
    {
        _userInfoInMemory.Remove(Context.User!.Identity!.Name);
        await Clients.AllExcept(new List<string> { Context.ConnectionId }).SendAsync(
               "UserLeft",
               Context.User.Identity.Name
               );
    }

    public async Task Join()
    {
        if (!_userInfoInMemory.AddUpdate(Context.User!.Identity!.Name, Context.ConnectionId))
        {
            // new user
            // var list = _userInfoInMemory.GetAllUsersExceptThis(Context.User.Identity.Name).ToList();
            await Clients.AllExcept(new List<string> { Context.ConnectionId }).SendAsync(
                "NewOnlineUser",
                _userInfoInMemory.GetUserInfo(Context.User.Identity.Name)
                );
        }
        else
        {
            // existing user joined again
            
        }

        await Clients.Client(Context.ConnectionId).SendAsync(
            "Joined",
            _userInfoInMemory.GetUserInfo(Context.User.Identity.Name)
            );

        await Clients.Client(Context.ConnectionId).SendAsync(
            "OnlineUsers",
            _userInfoInMemory.GetAllUsersExceptThis(Context.User!.Identity!.Name)
        );
    }

    public Task SendDirectMessage(string message, string targetUserName)
    {
        var userInfoSender = _userInfoInMemory.GetUserInfo(Context.User!.Identity!.Name);
        var userInfoReciever = _userInfoInMemory.GetUserInfo(targetUserName);
        return Clients.Client(userInfoReciever.ConnectionId).SendAsync("SendDM", message, userInfoSender);
    }
}
