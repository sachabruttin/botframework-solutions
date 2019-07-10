﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HospitalitySkill.Models;
using HospitalitySkill.Responses.CheckOut;
using HospitalitySkill.Services;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Dialogs.Choices;
using Microsoft.Bot.Builder.Solutions.Responses;

namespace HospitalitySkill.Dialogs
{
    public class CheckOutDialog : HospitalityDialogBase
    {
        private string _email;

        public CheckOutDialog(
            BotSettings settings,
            BotServices services,
            ResponseManager responseManager,
            ConversationState conversationState,
            UserState userState,
            IBotTelemetryClient telemetryClient)
            : base(nameof(CheckOutDialog), settings, services, responseManager, conversationState, userState, telemetryClient)
        {
            var checkout = new WaterfallStep[]
            {
                CheckOutPrompt,
                EmailPrompt,
                CheckOutConfirmed
            };

            AddDialog(new WaterfallDialog(nameof(CheckOutDialog), checkout));
            AddDialog(new ChoicePrompt(DialogIds.CheckOutPrompt, ValidateCheckOutAsync));
            AddDialog(new TextPrompt(DialogIds.EmailPrompt, ValidateEmailAsync));
        }

        private async Task<DialogTurnResult> CheckOutPrompt(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            var state = await StateAccessor.GetAsync(sc.Context, () => new HospitalitySkillState());

            var options = new PromptOptions()
            {
                Choices = new List<Choice>(),
            };

            options.Choices.Add(new Choice("Yes"));
            options.Choices.Add(new Choice("No"));

            // confirm user wants to check out
            return await sc.PromptAsync(DialogIds.CheckOutPrompt, new PromptOptions()
            {
                Prompt = ResponseManager.GetResponse(CheckOutResponses.ConfirmCheckOut),
                RetryPrompt = ResponseManager.GetResponse(CheckOutResponses.RetryConfirmCheckOut),
                Choices = options.Choices
            });
        }

        private async Task<bool> ValidateCheckOutAsync(PromptValidatorContext<FoundChoice> promptContext, CancellationToken cancellationToken)
        {
            var userState = await UserStateAccessor.GetAsync(promptContext.Context, () => new HospitalityUserSkillState());

            if (promptContext.Recognized.Succeeded && promptContext.Recognized.Value != null)
            {
                string response = promptContext.Recognized.Value.Value;
                if (response.Equals("Yes"))
                {
                    // TODO process check out request here
                    // set checkout value
                    userState.CheckedOut = true;
                    return await Task.FromResult(true);
                }
                else if (response.Equals("No"))
                {
                    // if no just don't set state
                    return await Task.FromResult(true);
                }
            }

            return await Task.FromResult(false);
        }

        private async Task<DialogTurnResult> EmailPrompt(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            var userState = await UserStateAccessor.GetAsync(sc.Context, () => new HospitalityUserSkillState());
            if (userState.CheckedOut == true)
            {
                // prompt for email to send receipt to
                return await sc.PromptAsync(DialogIds.EmailPrompt, new PromptOptions()
                {
                    Prompt = ResponseManager.GetResponse(CheckOutResponses.EmailPrompt),
                    RetryPrompt = ResponseManager.GetResponse(CheckOutResponses.InvalidEmailPrompt)
                });
            }

            return await sc.NextAsync();
        }

        private async Task<bool> ValidateEmailAsync(PromptValidatorContext<string> promptContext, CancellationToken cancellationToken)
        {
            var userState = await UserStateAccessor.GetAsync(promptContext.Context, () => new HospitalityUserSkillState());

            if (userState.CheckedOut == true)
            {
                // check for valid email input
                string response = promptContext.Recognized?.Value;

                if (promptContext.Recognized.Succeeded && !string.IsNullOrWhiteSpace(response) && new EmailAddressAttribute().IsValid(response))
                {
                    _email = response;
                    return await Task.FromResult(true);
                }

                return await Task.FromResult(false);
            }

            return await Task.FromResult(true);
        }

        private async Task<DialogTurnResult> CheckOutConfirmed(WaterfallStepContext sc, CancellationToken cancellationToken)
        {
            var userState = await UserStateAccessor.GetAsync(sc.Context, () => new HospitalityUserSkillState());
            var state = await StateAccessor.GetAsync(sc.Context, () => new HospitalitySkillState());

            if (userState.CheckedOut == true)
            {
                var tokens = new StringDictionary
            {
                { "Email", _email },
            };

                // checked out confirmation message
                await sc.Context.SendActivityAsync(ResponseManager.GetResponse(CheckOutResponses.SendEmailMessage, tokens));
                await sc.Context.SendActivityAsync(ResponseManager.GetResponse(CheckOutResponses.CheckOutSuccess));

                // if user is checked out shouldn't be allowed to do anything else
            }
            else
            {
                // didn't check out, help with something else
                await sc.Context.SendActivityAsync(ResponseManager.GetResponse(CheckOutResponses.CheckOutError));
            }

            return await sc.EndDialogAsync();
        }

        private class DialogIds
        {
            public const string CheckOutPrompt = "checkOutPrompt";
            public const string EmailPrompt = "emailPrompt";
        }
    }
}