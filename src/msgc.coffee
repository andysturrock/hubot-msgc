# Loosely based on https://github.com/garrettheel/hubot-websocket

{Robot, Adapter, TextMessage} = require 'hubot'
http                          = require 'http'
WebSocketServer               = require('ws').Server
HttpClient                    = require 'scoped-http-client'

PROXY_HOST = process.env.PROXY_HOST
PROXY_PORT = process.env.PROXY_PORT
HUBOT_MSGC_LISTEN_PORT = process.env.HUBOT_MSGC_LISTEN_PORT
unless HUBOT_MSGC_LISTEN_PORT?
  console.log "Missing HUBOT_MSGC_LISTEN_PORT environment variable.  Please set and try again."
  process.exit(1)

class MicrosoftGroupChatAdapter extends Adapter

  constructor: (robot) ->
    super robot

  send: (envelope, strings...) ->
    @robot.logger.debug("send")

    out = ""
    out = ("#{str}\n" for str in strings)
    textMessage = {
      "type": "text",
      "id": envelope.message.id,
      "username": envelope.user.id,
      "room": envelope.room,
      "text": out.join('')
    }
    json = JSON.stringify(textMessage)

    @wss.clients.forEach (client) =>
      client.send(json)

  emote: (envelope, strings...) ->
    @robot.logger.debug("emote")
    @send envelope, strings

  reply: (envelope, strings...) ->
    @robot.logger.debug("reply")
    @send envelope, strings

  _onTextMessage: (user, id, text) =>
    @robot.logger.debug("Received text message id #{id} from #{user.name} => #{text}")
    @receive new TextMessage(user, text, id)

  onMessage: (message) =>
    @robot.logger.debug("Received message #{message}")
    message = JSON.parse message
    user = @robot.brain.userForId(message.username)
    user.room = message.room
    switch message.type
        when "text" then @_onTextMessage(user, message.id, message.text)
        else @robot.logger.error("Unexpected message type #{message.type}")

  run: ->
    # Hack robot to inject proxy settings
    if (PROXY_HOST)
      @robot.http = @http

    @wss = new WebSocketServer {port: HUBOT_MSGC_LISTEN_PORT}
    @wss.on 'connection', (ws) =>
      @robot.logger.info("Websocket connection opened")
      ws.on 'message', @onMessage

    @robot.logger.info("Connected to hubot")
    @emit "connected"

exports.use = (robot) ->
  new MicrosoftGroupChatAdapter robot
