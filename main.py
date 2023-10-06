import openai

openai.api_key = ''

prompt = "What is the meaning of life?"

response = openai.Completion.create(
  engine='text-davinci-003',
  prompt=prompt,
  max_tokens=50,
  temperature=0.7,
  n=1,
  stop=None,
)

chatgpt_response = response.choices[0].text.strip()
print(chatgpt_response)
