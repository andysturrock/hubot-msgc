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
    @robot.logger.info "Constructed!"

  send: (envelope, strings...) ->
    console.log("**** send ****")
    @robot.logger.info("**** send ****")
    @wss.clients.forEach (client) =>
      client.send(JSON.stringify(strings))

  emote: (envelope, strings...) ->
    console.log("**** emote ****")
    @robot.logger.info("**** emote ****")
    @send envelope, "* #{str}" for str in strings

  reply: (envelope, strings...) ->
    console.log("**** reply ****")
    @robot.logger.info("**** reply ****")
    strings = strings.map (s) -> "#{envelope.user.name}: #{s}"
    @send envelope, strings...

  on_message: (e) =>
    console.log("**** on_message ****")
    @robot.logger.info("**** on_message #{e} ****")
    e = JSON.parse e
    @robot.logger.info("**** on_message now e = #{e} ****")
    @robot.logger.info("**** on_message now e.message = #{e.message} ****")
    user = @robot.brain.userForId(e.user, {name: e.user, room: e.room})
    @receive new TextMessage(user, e.message, 'messageId')

  on_heartbeat: (e) =>
    console.log("**** on_heartbeat ****")
    @robot.logger.info("**** on_heartbeat ****")

  run: ->
    # Hack robot to inject proxy settings
    if (PROXY_HOST)
      @robot.http = @http

    @wss = new WebSocketServer {port: 4773}
    @wss.on 'connection', (ws) =>
      console.log("**** connected ****")
      @robot.logger.info("**** connected ****")
      ws.on 'message', @on_message
      ws.on 'heartbeat', @on_heartbeat

    console.log("**** Woohoo ****")
    @robot.logger.info("**** Woohoo ****")
    @emit "connected"

exports.use = (robot) ->
  new MicrosoftGroupChatAdapter robot
