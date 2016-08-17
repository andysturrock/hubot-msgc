# Loosely based on https://github.com/garrettheel/hubot-websocket

{Robot, Adapter, TextMessage} = require 'hubot'
http                          = require 'http'
WebSocketServer               = require('ws').Server
HttpClient                    = require 'scoped-http-client'

PROXY_HOST = process.env.PROXY_HOST
PROXY_PORT = process.env.PROXY_PORT

class MicrosoftGroupChatAdapter extends Adapter

  constructor: (robot) ->
    super robot

  send: (envelope, strings...) ->
    console.log("**** send ****")
    console.log(envelope)
    console.log(strings)
    console.log("**** send ****")
    @wss.clients.forEach (client) =>
      client.send(JSON.stringify(strings))

  emote: (envelope, strings...) ->
    console.log("**** emote ****")
    @robot.logger.info("**** emote ****")
    @send envelope, "* #{str}" for str in strings

  reply: (envelope, strings...) ->
    console.log("**** reply ****")
    console.log(envelope)
    console.log(strings)
    console.log("**** reply ****")
    strings = strings.map (s) -> "#{envelope.user.name}: #{s}"
    @send envelope, strings...

  _on_text_message: (user, text) =>
    @robot.logger.debug("Received text message from #{user.name} => #{text}")
    @receive new TextMessage(user, text)

  on_message: (message) =>
    @robot.logger.debug("Received message #{message}")
    message = JSON.parse message
    user = @robot.brain.userForId(message.username)
    switch message.type
        when "text" then @_on_text_message(user, message.text)
        else @robot.logger.error("Unexpected message type #{message.type}")

  on_heartbeat: (e) =>
    @robot.logger.debug("heartbeat")

  run: ->
    # Hack robot to inject proxy settings
    if (PROXY_HOST)
      @robot.http = @http

    @wss = new WebSocketServer {port: 4773}
    @wss.on 'connection', (ws) =>
      @robot.logger.info("Websocket connection opened")
      ws.on 'message', @on_message
      ws.on 'heartbeat', @on_heartbeat

    @robot.logger.info("Connected to hubot")
    @emit "connected"

exports.use = (robot) ->
  new MicrosoftGroupChatAdapter robot
